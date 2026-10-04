using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace MuteMyMic
{
    class MicInfo
    {
        public string Id;
        public string Name;
    }

    enum CommandResult { Done, NoMics, Failed }

    // Owns every Core Audio call. All of them run on ONE background thread, so a slow or
    // hung audio driver can never freeze the tray, the hotkeys or the mouse.
    //
    // Windows tells us about changes (IMMNotificationClient for devices plugged in / out,
    // IAudioEndpointVolumeCallback for mute changes), so nothing is polled in a tight loop;
    // a slow safety re-check every 10 s also rebuilds everything if the audio service restarted.
    //
    // Results go back to the UI thread through SynchronizationContext.Post (never Send),
    // so the UI may wait for this thread (with a timeout) without any risk of deadlock.
    sealed class AudioService : IDisposable
    {
        /// <summary>UI thread: the app's mute state changed (by a command or outside the app).</summary>
        public event Action<bool> StateChanged;

        /// <summary>UI thread: a user command finished (action, result, muted afterwards, error text).</summary>
        public event Action<MicAction, CommandResult, bool, string> CommandCompleted;

        const int SafetyCheckMs = 10000;

        readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        readonly Thread thread;
        readonly SynchronizationContext ui;
        int resyncQueued;                                   // 1 while a re-check is already waiting in the queue
        readonly ConcurrentDictionary<string, byte> goneIds = new ConcurrentDictionary<string, byte>(); // unplugged since the last re-check

        // ---- state below is touched ONLY on the worker thread ----
        Endpoints endpoints;
        bool muted;
        HashSet<string> excluded;
        Dictionary<string, bool> lastSeen = new Dictionary<string, bool>(); // handled mics after our last action

        public AudioService(IEnumerable<string> excludedIds)
        {
            ui = SynchronizationContext.Current;  // created on the UI thread
            excluded = new HashSet<string>(excludedIds);
            thread = new Thread(Run) { IsBackground = true, Name = "MuteMyMic audio" };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        // ================================================================ public API (UI thread)

        /// <summary>First thing after start. Started with Windows = microphones always on.</summary>
        public void Initialize(bool autostarted, bool startMuted)
        {
            Enqueue(() =>
            {
                Dictionary<string, bool> handled = ReadHandled();
                if (autostarted)
                {
                    SetAll(handled, false);
                    muted = false;
                }
                else
                {
                    muted = handled.Count > 0 && AllMuted(handled);
                    if (startMuted && !muted && handled.Count > 0)
                    {
                        SetAll(handled, true);
                        muted = true;
                    }
                }
                lastSeen = handled;
                PostState();
            });
        }

        /// <summary>Hotkey / tray click / command line. The toggle is decided here, in order,
        /// so two quick presses always mean "off, then on".</summary>
        public void Do(MicAction action, bool fromUser)
        {
            Enqueue(() =>
            {
                bool target = action == MicAction.Mute || (action == MicAction.Toggle && !muted);
                CommandResult result;
                string error = null;
                try
                {
                    Dictionary<string, bool> handled = ReadHandled();
                    if (handled.Count == 0)
                    {
                        result = CommandResult.NoMics;
                    }
                    else
                    {
                        result = SetAll(handled, target) ? CommandResult.Done : CommandResult.Failed;
                        if (result == CommandResult.Done) muted = target;
                        lastSeen = handled;
                    }
                }
                catch (Exception ex)
                {
                    ResetEndpoints();
                    result = CommandResult.Failed;
                    error = ex.Message;
                }
                PostState();
                if (fromUser) Post(() => { var h = CommandCompleted; if (h != null) h(action, result, muted, error); });
            });
        }

        /// <summary>Mutes for the lock screen; reports back whether it actually changed anything.</summary>
        public void MuteForLock(Action<bool> done)
        {
            Enqueue(() =>
            {
                bool changed = false;
                if (!muted)
                {
                    Dictionary<string, bool> handled = ReadHandled();
                    if (handled.Count > 0 && SetAll(handled, true))
                    {
                        muted = true;
                        changed = true;
                        lastSeen = handled;
                    }
                }
                PostState();
                Post(() => done(changed));
            });
        }

        /// <summary>The user changed which microphones are handled.</summary>
        public void SetExcluded(IEnumerable<string> ids)
        {
            var next = new HashSet<string>(ids);
            Enqueue(() =>
            {
                if (muted)
                {
                    // Release mics that are no longer handled, mute the newly handled ones.
                    Dictionary<string, bool> all = OpenEndpoints().Refresh(goneIds);
                    foreach (var kv in all)
                    {
                        bool wasHandled = !excluded.Contains(kv.Key), isHandled = !next.Contains(kv.Key);
                        if (wasHandled && !isHandled && kv.Value) endpoints.SetMute(kv.Key, false);
                        if (!wasHandled && isHandled && !kv.Value) endpoints.SetMute(kv.Key, true);
                    }
                }
                excluded = next;
                lastSeen = ReadHandled();
                PostState();
            });
        }

        /// <summary>Microphone list for the settings window; waits at most timeoutMs.</summary>
        public List<MicInfo> ListMics(int timeoutMs)
        {
            return Invoke(() => OpenEndpoints().List(goneIds), timeoutMs, new List<MicInfo>());
        }

        /// <summary>Switch every handled mic on and wait (exit, sign-out, shutdown).</summary>
        public void UnmuteAllAndWait(int timeoutMs)
        {
            Invoke(() =>
            {
                Dictionary<string, bool> handled = ReadHandled();
                SetAll(handled, false);
                muted = false;
                lastSeen = handled;
                return true;
            }, timeoutMs, false);
        }

        public void Dispose()
        {
            try { queue.CompleteAdding(); } catch (ObjectDisposedException) { }
            thread.Join(2000); // a hung driver must not block exit; the thread is a background thread
        }

        // ================================================================ worker thread

        void Run()
        {
            while (true)
            {
                Action work;
                bool got;
                try { got = queue.TryTake(out work, SafetyCheckMs); }
                catch (InvalidOperationException) { break; }     // CompleteAdding was called
                if (!got)
                {
                    if (queue.IsAddingCompleted) break;
                    work = Resync;                                // safety re-check
                }
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    // Never let one failure kill the audio thread: rebuild and carry on.
                    Log.Write(ex);
                    ResetEndpoints();
                }
            }
            ResetEndpoints();
        }

        void Enqueue(Action work)
        {
            try { queue.Add(work); }
            catch (InvalidOperationException) { }   // shutting down (also covers ObjectDisposedException)
        }

        T Invoke<T>(Func<T> work, int timeoutMs, T fallback)
        {
            var done = new ManualResetEvent(false);
            T result = fallback;
            Enqueue(() =>
            {
                try { result = work(); }
                finally { done.Set(); }
            });
            bool finished = done.WaitOne(timeoutMs);
            // Not disposed on timeout: the worker may still Set() it later.
            if (finished) done.Close();
            return finished ? result : fallback;
        }

        void Post(Action a)
        {
            if (ui != null) ui.Post(_ => a(), null);
        }

        bool postedMuted, postedOnce;

        void PostState()
        {
            if (postedOnce && postedMuted == muted) return;
            postedOnce = true;
            postedMuted = muted;
            bool m = muted;
            Post(() => { var h = StateChanged; if (h != null) h(m); });
        }

        // Called from Windows audio threads: must be quick and must not touch worker state.
        void OnAudioChanged(string goneId)
        {
            if (goneId != null) goneIds[goneId] = 0;
            if (Interlocked.Exchange(ref resyncQueued, 1) == 0)
                Enqueue(() => { Interlocked.Exchange(ref resyncQueued, 0); Resync(); });
        }

        // Re-reads every microphone and reacts to what changed outside the app.
        void Resync()
        {
            Dictionary<string, bool> handled;
            try { handled = ReadHandled(); }
            catch (Exception ex)
            {
                Log.Write(ex);
                ResetEndpoints();   // audio service restarted / driver failed: rebuild next time
                return;
            }

            if (muted)
            {
                // A mic we had muted, seen continuously since, is now on: the user switched it on
                // in Windows or another app — follow that instead of fighting it.
                foreach (var kv in handled)
                {
                    bool wasMuted;
                    if (!kv.Value && lastSeen.TryGetValue(kv.Key, out wasMuted) && wasMuted)
                    {
                        muted = false;
                        lastSeen = handled;
                        PostState();
                        return;
                    }
                }
                // Anything else that is on was plugged in (or reconnected) after muting: mute it.
                foreach (var kv in new List<KeyValuePair<string, bool>>(handled))
                    if (!kv.Value && endpoints.SetMute(kv.Key, true)) handled[kv.Key] = true;
            }
            else if (handled.Count > 0 && AllMuted(handled))
            {
                muted = true;   // every mic was muted outside the app
            }
            lastSeen = handled;
            PostState();
        }

        /// <summary>Mute state of the handled (not excluded) microphones.</summary>
        Dictionary<string, bool> ReadHandled()
        {
            Dictionary<string, bool> all = OpenEndpoints().Refresh(goneIds);
            // A mic that was unplugged since the last look is "new" if it is back: forget its old state.
            foreach (var id in new List<string>(goneIds.Keys))
            {
                byte dummy;
                goneIds.TryRemove(id, out dummy);
                lastSeen.Remove(id);
            }
            var handled = new Dictionary<string, bool>();
            foreach (var kv in all)
                if (!excluded.Contains(kv.Key)) handled[kv.Key] = kv.Value;
            return handled;
        }

        bool SetAll(Dictionary<string, bool> handled, bool mute)
        {
            bool allOk = true;
            foreach (var id in new List<string>(handled.Keys))
            {
                if (handled[id] == mute) continue;
                if (endpoints.SetMute(id, mute)) handled[id] = mute;
                else allOk = false;
            }
            return allOk;
        }

        static bool AllMuted(Dictionary<string, bool> states)
        {
            foreach (bool m in states.Values)
                if (!m) return false;
            return true;
        }

        Endpoints OpenEndpoints()
        {
            if (endpoints == null) endpoints = new Endpoints(OnAudioChanged);
            return endpoints;
        }

        void ResetEndpoints()
        {
            if (endpoints != null)
            {
                try { endpoints.Dispose(); } catch (Exception ex) { Log.Write(ex); }
                endpoints = null;
            }
            lastSeen = new Dictionary<string, bool>(); // after a rebuild every mic counts as new
        }
    }

    // Core Audio objects for the capture devices. Worker thread only.
    sealed class Endpoints : IDisposable
    {
        const int eCapture = 1;
        const int DEVICE_STATE_ACTIVE = 1;
        const int CLSCTX_ALL = 23;
        const int STGM_READ = 0;
        const ushort VT_LPWSTR = 31;
        static readonly Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static PropertyKey PKEY_Device_FriendlyName =
            new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

        /// <summary>Event context we pass to SetMute, so our own changes are not mistaken for outside ones.</summary>
        public static readonly Guid OwnContext = new Guid("6A1D9F3E-2B47-4C8E-9A51-3F0E7C2D8B14");

        sealed class Entry
        {
            public IAudioEndpointVolume Volume;
            public VolumeCallback Callback;
            public string Name;
        }

        readonly Action<string> onChanged;
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        IMMDeviceEnumerator enumerator;
        DeviceCallback deviceCallback;

        public Endpoints(Action<string> onChanged)
        {
            this.onChanged = onChanged;
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            try
            {
                deviceCallback = new DeviceCallback(onChanged);
                if (enumerator.RegisterEndpointNotificationCallback(deviceCallback) != 0) deviceCallback = null;
            }
            catch
            {
                deviceCallback = null; // still usable: the 10 s safety check covers plug/unplug
            }
        }

        /// <summary>
        /// Brings the list of active microphones up to date (keeping one volume object and one
        /// change callback per mic) and returns id → muted. Throws if the audio service is gone.
        /// </summary>
        public Dictionary<string, bool> Refresh(ConcurrentDictionary<string, byte> goneIds)
        {
            var states = new Dictionary<string, bool>();
            var present = new HashSet<string>();
            IMMDeviceCollection devices = null;
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, out devices));
                uint count;
                Marshal.ThrowExceptionForHR(devices.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        if (devices.Item(i, out device) != 0 || device == null) continue;
                        string id;
                        if (device.GetId(out id) != 0 || id == null) continue;
                        present.Add(id);

                        Entry e;
                        if (!entries.TryGetValue(id, out e))
                        {
                            e = Open(device, id);
                            if (e == null) continue;
                            entries[id] = e;
                        }

                        bool m;
                        if (e.Volume.GetMute(out m) == 0)
                        {
                            states[id] = m;
                        }
                        else
                        {
                            // Device went away between the listing and this call.
                            Close(id, e);
                            entries.Remove(id);
                            present.Remove(id);
                            goneIds[id] = 0;
                        }
                    }
                    finally
                    {
                        Release(device);
                    }
                }
            }
            finally
            {
                Release(devices);
            }

            foreach (var id in new List<string>(entries.Keys))
            {
                if (present.Contains(id)) continue;
                Close(id, entries[id]);
                entries.Remove(id);
                goneIds[id] = 0;
            }
            return states;
        }

        public List<MicInfo> List(ConcurrentDictionary<string, byte> goneIds)
        {
            Dictionary<string, bool> states = Refresh(goneIds);
            var list = new List<MicInfo>();
            foreach (var id in states.Keys) list.Add(new MicInfo { Id = id, Name = entries[id].Name ?? id });
            return list;
        }

        public bool SetMute(string id, bool mute)
        {
            return SetMute(id, mute, OwnContext);
        }

        /// <summary>context = OwnContext for the app's own changes (their notifications are ignored).</summary>
        public bool SetMute(string id, bool mute, Guid context)
        {
            Entry e;
            if (!entries.TryGetValue(id, out e)) return false;
            return e.Volume.SetMute(mute, ref context) >= 0; // S_OK or S_FALSE (already in that state)
        }

        Entry Open(IMMDevice device, string id)
        {
            Guid iid = IID_IAudioEndpointVolume;
            object o;
            if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0) return null;
            var volume = o as IAudioEndpointVolume;
            if (volume == null) { Release(o); return null; }

            var e = new Entry { Volume = volume, Name = FriendlyName(device) };
            var cb = new VolumeCallback(onChanged);
            try { if (volume.RegisterControlChangeNotify(cb) == 0) e.Callback = cb; } catch { }
            return e;
        }

        static void Close(string id, Entry e)
        {
            try { if (e.Callback != null) e.Volume.UnregisterControlChangeNotify(e.Callback); } catch { }
            Release(e.Volume);
        }

        public void Dispose()
        {
            foreach (var kv in entries) Close(kv.Key, kv.Value);
            entries.Clear();
            if (enumerator != null)
            {
                try { if (deviceCallback != null) enumerator.UnregisterEndpointNotificationCallback(deviceCallback); } catch { }
                Release(enumerator);
                enumerator = null;
            }
        }

        static string FriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            try
            {
                if (device.OpenPropertyStore(STGM_READ, out store) != 0 || store == null) return null;
                PROPVARIANT value;
                if (store.GetValue(ref PKEY_Device_FriendlyName, out value) != 0) return null;
                try
                {
                    return value.vt == VT_LPWSTR ? Marshal.PtrToStringUni(value.p) : null;
                }
                finally
                {
                    PropVariantClear(ref value);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                Release(store);
            }
        }

        static void Release(object o)
        {
            try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
            catch { }
        }

        // ---- COM interop declarations (methods in vtable order) ----


        [StructLayout(LayoutKind.Sequential)]
        struct PROPVARIANT
        {
            public ushort vt, r1, r2, r3;
            public IntPtr p;
            public IntPtr p2;
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PROPVARIANT pvar);

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                                       [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PROPVARIANT value);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
            [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float levelDB, ref Guid ctx);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDB, ref Guid ctx);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }

    // ---- Callbacks Windows calls on its own threads. They must be public for COM, and must never throw or block. ----

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int newState);
        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr notifyData);
    }

    /// <summary>Microphones plugged in / unplugged / disabled.</summary>
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class DeviceCallback : IMMNotificationClient
    {
        const int DEVICE_STATE_ACTIVE = 1;
        readonly Action<string> changed;
        internal DeviceCallback(Action<string> changed) { this.changed = changed; }

        // Capture endpoint ids start with {0.0.1. (playback ones with {0.0.0.)
        static bool IsMic(string id) { return id != null && id.StartsWith("{0.0.1.", StringComparison.Ordinal); }

        public void OnDeviceStateChanged(string id, int newState)
        {
            if (IsMic(id)) Safe(newState == DEVICE_STATE_ACTIVE ? null : id);
        }
        public void OnDeviceAdded(string id) { if (IsMic(id)) Safe(null); }
        public void OnDeviceRemoved(string id) { if (IsMic(id)) Safe(id); }
        public void OnDefaultDeviceChanged(int flow, int role, string id) { }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }

        void Safe(string goneId)
        {
            try { changed(goneId); } catch { }
        }
    }

    /// <summary>A microphone was muted or unmuted (by anyone).</summary>
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class VolumeCallback : IAudioEndpointVolumeCallback
    {
        readonly Action<string> changed;
        internal VolumeCallback(Action<string> changed) { this.changed = changed; }

        public int OnNotify(IntPtr data)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA starts with the event-context GUID.
                var ctx = (Guid)Marshal.PtrToStructure(data, typeof(Guid));
                if (ctx != Endpoints.OwnContext) changed(null); // our own changes are already known
            }
            catch { }
            return 0;
        }
    }
}