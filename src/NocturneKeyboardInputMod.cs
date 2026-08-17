using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppname_H;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(NocturneKeyboardInput.NocturneKeyboardInputMod), "Nocturne Keyboard Input", "0.18.2-auto-yes-quiet", "Gray Ghost")]
[assembly: MelonGame(null, "smt3hd")]

namespace NocturneKeyboardInput
{
    public sealed class NocturneKeyboardInputMod : MelonMod
    {
        private const string ConfigFileName = "NocturneKeyboardInput.cfg";
        private string _surname = "嘉嶋";
        private string _givenName = "尚紀";
        private string _nickname = "人修羅";
        private bool _nicknameScreenObserved;
        private bool _finalNamesApplied;
        private int _postNameFrames;
        private int _lastObservedEntryType = -1;
        private bool _dummyArmed;
        private string _stageKey = "";
        private int _stageStableFrames;
        private int _lastCount = -1;
        private int _awaitingIncrementFrames;
        private int _terminalStableFrames;
        private bool _startRequestedForStage;
        private int _confirmStableFrames;
        private bool _confirmRequested;
        private string HelperDirectory => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneKeyboardInput");
        private string HelperExePath => Path.Combine(HelperDirectory, "NameInputHelper.exe");
        private string HelperResponsePath => Path.Combine(HelperDirectory, "name-response.txt");
        private string _lastEndState = "";
        private const int SwMinimize = 6;

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);


        public override void OnInitializeMelon()
        {
            LoadOrCreateConfig();
            HarmonyInstance.PatchAll(typeof(NocturneKeyboardInputMod).Assembly);
            LoggerInstance.Msg("[NocturneKeyboardInput] Loaded quiet automatic-YES v0.18.2");
            LoggerInstance.Msg($"[NocturneKeyboardInput] Target names: Surname=\"{_surname}\", GivenName=\"{_givenName}\", Nickname=\"{_nickname}\"");
            LoggerInstance.Msg("[NocturneKeyboardInput] At the first surname palette, keep the cursor on a normal character and press F8 to open the Japanese IME input window.");
        }

        public override void OnUpdate()
        {
            try
            {
                ApplyPendingSelection();
                if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
                {
                    OpenNameWindow();
                }

                nmeData_t? work = TryGetNameWork();
                sbyte process = TryGetNameProcess();
                if (work != null && process == 1)
                {
                    ObserveEndState(work);
                    if (_lastObservedEntryType != work.EntryType)
                    {
                        _lastObservedEntryType = work.EntryType;
                        ObserveImeState(work);
                    }
                    UpdateGuardedFinalConfirm(work);
                    if (_dummyArmed) UpdateGuardedDummyInput(work);
                    if (work.EntryType == 2)
                    {
                        _nicknameScreenObserved = true;
                        _postNameFrames = 0;
                    }
                    return;
                }

                _lastObservedEntryType = -1;
                ResetDummyStageTracking();

                if (_nicknameScreenObserved && !_finalNamesApplied)
                {
                    _postNameFrames++;
                    if (_postNameFrames >= 2) TryApplyConfiguredNames();
                }
            }
            catch (Exception ex)
            {
                LogException("OnUpdate failed", ex);
            }
        }

        private void ObserveEndState(nmeData_t work)
        {
            try
            {
                int funcIndex = -1;
                try
                {
                    if (work.FuncCursor != null && work.FuncCursor.CursorPos != null) funcIndex = work.FuncCursor.CursorPos.Index;
                }
                catch { }
                int yesNoIndex = -1;
                try
                {
                    if (work.YesNoCursor != null && work.YesNoCursor.CursorPos != null) yesNoIndex = work.YesNoCursor.CursorPos.Index;
                }
                catch { }
                string state = $"EntryType={work.EntryType}, CfmFlag={work.CfmFlag}, InputPos={work.InputPos}, FuncFlag={work.FuncFlag}, FuncIndex={funcIndex}, YesNoIndex={yesNoIndex}, nameEndflag={Nme_Update.nameEndflag}, StartEndNameflag={Nme_Update.StartEndNameflag}";
                if (state == _lastEndState) return;
                _lastEndState = state;
                LoggerInstance.Msg($"[NocturneKeyboardInput][START-OBSERVE] {state}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[NocturneKeyboardInput][START-OBSERVE] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void OpenNameWindow()
        {
            try
            {
                if (!File.Exists(HelperExePath))
                {
                    LoggerInstance.Error($"[NocturneKeyboardInput][WINDOW] Helper not found: {HelperExePath}");
                    return;
                }
                Directory.CreateDirectory(HelperDirectory);
                if (File.Exists(HelperResponsePath)) File.Delete(HelperResponsePath);
                var start = new ProcessStartInfo
                {
                    FileName = HelperExePath,
                    UseShellExecute = false,
                    WorkingDirectory = HelperDirectory
                };
                start.ArgumentList.Add(Path.Combine(MelonEnvironment.UserDataDirectory, ConfigFileName));
                start.ArgumentList.Add(HelperResponsePath);
                IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
                start.ArgumentList.Add(gameWindow.ToInt64().ToString());
                Process.Start(start);
                if (gameWindow != IntPtr.Zero) ShowWindowAsync(gameWindow, SwMinimize);
                LoggerInstance.Msg("[NocturneKeyboardInput][WINDOW] External helper started.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"[NocturneKeyboardInput][WINDOW] Helper launch failed: {ex}");
            }
        }

        private void ApplyPendingSelection()
        {
            if (!File.Exists(HelperResponsePath)) return;
            string[] values;
            try
            {
                values = File.ReadAllLines(HelperResponsePath, Encoding.UTF8);
                File.Delete(HelperResponsePath);
            }
            catch { return; }
            if (values.Length < 3) return;
            _surname = values[0].Trim();
            _givenName = values[1].Trim();
            _nickname = values[2].Trim();
            if (_surname.Length == 0 || _surname.Length > 4 || _givenName.Length == 0 || _givenName.Length > 4 || _nickname.Length == 0 || _nickname.Length > 8)
            {
                LoggerInstance.Error("[NocturneKeyboardInput][WINDOW] Helper response failed length validation.");
                return;
            }
            _dummyArmed = true;
            _nicknameScreenObserved = false;
            _finalNamesApplied = false;
            _postNameFrames = 0;
            _confirmStableFrames = 0;
            _confirmRequested = false;
            ResetDummyStageTracking();
            SaveConfig();
            LoggerInstance.Msg($"[NocturneKeyboardInput][WINDOW] External values accepted and armed: Surname=\"{_surname}\", GivenName=\"{_givenName}\", Nickname=\"{_nickname}\"");
        }

        private void SaveConfig()
        {
            try
            {
                string path = Path.Combine(MelonEnvironment.UserDataDirectory, ConfigFileName);
                File.WriteAllLines(path, new[]
                {
                    "# UTF-8. Values selected in the F8 input window are saved here.",
                    $"Surname={_surname}",
                    $"GivenName={_givenName}",
                    $"Nickname={_nickname}"
                }, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[NocturneKeyboardInput] Config save failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void UpdateGuardedDummyInput(nmeData_t work)
        {
            if (work.CfmFlag != 0) return;
            if (work.EntryType != 0 && work.EntryType != 2) return;

            sbyte inputPos;
            try { inputPos = Nme_Update.nmeGetInputCharPos(ref work); }
            catch (Exception ex)
            {
                AbortDummyInput($"Could not obtain input position: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            string key = $"{work.TargetNo}:{work.EntryMode}:{work.EntryType}";
            if (_stageKey != key)
            {
                _stageKey = key;
                _stageStableFrames = 0;
                _lastCount = -1;
                _awaitingIncrementFrames = 0;
                _terminalStableFrames = 0;
                _startRequestedForStage = false;
                LoggerInstance.Msg($"[NocturneKeyboardInput][DUMMY-POC] Stage observed: {key}");
                return;
            }

            if (_stageStableFrames < 20)
            {
                _stageStableFrames++;
                return;
            }

            if (inputPos < 0)
            {
                AbortDummyInput($"Invalid input position: pos={inputPos}");
                return;
            }

            // EntryType 0 uses one raw eight-slot cursor (surname 0..3, given name 4..7).
            // nmeGetInputCharPos() intentionally changes to a field-relative value at slot 4.
            // The nickname screen uses its adjusted/local position.
            int count = work.EntryType == 0 ? work.InputPos : inputPos;
            if (_surname.Length > 4 || _givenName.Length > 4)
            {
                AbortDummyInput($"Surname/given-name exceeds the four-slot UI: surname={_surname.Length}, given={_givenName.Length}");
                return;
            }

            DummyAction action;
            int terminal;
            if (work.EntryType == 0)
            {
                terminal = 4 + _givenName.Length;
                if (count < _surname.Length) action = DummyAction.Character;
                else if (count < 4) action = DummyAction.MoveRight;
                else if (count < terminal) action = DummyAction.Character;
                else action = DummyAction.None;
            }
            else
            {
                terminal = _nickname.Length;
                action = count < terminal ? DummyAction.Character : DummyAction.None;
            }

            if (action == DummyAction.None)
            {
                _lastCount = count;
                _awaitingIncrementFrames = 0;
                if (!_startRequestedForStage && work.CfmFlag == 0)
                {
                    _terminalStableFrames++;
                    if (_terminalStableFrames >= 20)
                    {
                        _startRequestedForStage = true;
                        CommonInputPatch.RequestOnce();
                        LoggerInstance.Msg($"[NocturneKeyboardInput][AUTO-START-INPUT] Queued one 0x1000 input: EntryType={work.EntryType}, InputPos={work.InputPos}");
                    }
                }
                return;
            }

            _terminalStableFrames = 0;

            if (_lastCount >= 0 && count <= _lastCount)
            {
                _awaitingIncrementFrames++;
                if (_awaitingIncrementFrames >= 3)
                    AbortDummyInput($"Input position did not increase after the requested original operation: stage={key}, count={count}");
                return;
            }

            _lastCount = count;
            _awaitingIncrementFrames = 0;
            try
            {
                if (action == DummyAction.Character) Nme_Update.nmeGetInputChar();
                else Nme_Update.nmeMoveInputCursor(1);
                LoggerInstance.Msg($"[NocturneKeyboardInput][DUMMY-POC] Requested {action}: stage={key}, before={count}, terminal={terminal}");
            }
            catch (Exception ex)
            {
                AbortDummyInput($"nmeGetInputChar failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void UpdateGuardedFinalConfirm(nmeData_t work)
        {
            // This is only the final Yes/No dialog after the nickname screen.
            if (!_dummyArmed || work.EntryType != 2 || work.CfmFlag != 1)
            {
                _confirmStableFrames = 0;
                return;
            }

            int yesNoIndex;
            try
            {
                if (work.YesNoCursor == null || work.YesNoCursor.CursorPos == null) return;
                yesNoIndex = work.YesNoCursor.CursorPos.Index;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[NocturneKeyboardInput][AUTO-YES] Cursor read failed: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            // The live confirmation screen maps its initially selected "Yes" item to index 0.
            // Never inject the decision input while the cursor points elsewhere.
            if (yesNoIndex != 0)
            {
                _confirmStableFrames = 0;
                return;
            }

            if (_confirmRequested) return;
            _confirmStableFrames++;
            if (_confirmStableFrames < 75) return;

            _confirmRequested = true;
            CommonInputPatch.RequestConfirmPair();
            LoggerInstance.Msg("[NocturneKeyboardInput][AUTO-YES] Queued two scoped checks after 75 stable frames at YesNoIndex=0.");
        }

        private enum DummyAction { None, Character, MoveRight }

        private void AbortDummyInput(string reason)
        {
            _dummyArmed = false;
            ResetDummyStageTracking();
            LoggerInstance.Error($"[NocturneKeyboardInput][DUMMY-POC] Aborted safely: {reason}");
        }

        private void ResetDummyStageTracking()
        {
            _stageKey = "";
            _stageStableFrames = 0;
            _lastCount = -1;
            _awaitingIncrementFrames = 0;
            _terminalStableFrames = 0;
            _startRequestedForStage = false;
        }

        private void ObserveImeState(nmeData_t work)
        {
            try
            {
                ImeKeyboard? ime = Nme_Update.keyboardObj_a;
                string imeText = ime == null ? "<null>" : (ime.InputText ?? "");
                bool imeOpen = ime != null && ime.IsOpenIME;
                bool imeCancel = ime != null && ime.IsCancel;
                sbyte keyboardProcess = 0;
                try { keyboardProcess = Nme_KeyBoard.nmeChkKeyBoardProcess(); }
                catch { }

                LoggerInstance.Msg(
                    $"[NocturneKeyboardInput][IME-OBSERVE] EntryType={work.EntryType}, EntryMode={work.EntryMode}, " +
                    $"NmeUpdate.keyboardUseflag={Nme_Update.keyboardUseflag}, KeyBordflag={Nme_Update.KeyBordflag}, " +
                    $"MojiInputflag={Nme_Update.MojiInputflag}, Lang={Nme_Update.Lang}, Num={Nme_Update.Num}, " +
                    $"keyboardObj={(Nme_Update.keyboardObj == null ? "null" : "alive")}, keyboardObj_a={(ime == null ? "null" : "alive")}, " +
                    $"ImeOpen={imeOpen}, ImeCancel={imeCancel}, ImeText=\"{imeText}\", " +
                    $"NmeKeyBoardProcess={keyboardProcess}, NmeKeyBoardUseflag={Nme_KeyBoard.KeyBoardUseflag}, " +
                    $"NmeNameKeyBoardflag={Nme_KeyBoard.NameKeyBoardflag}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[NocturneKeyboardInput][IME-OBSERVE] Observation failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void LoadOrCreateConfig()
        {
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, ConfigFileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllLines(path, new[]
                    {
                        "# UTF-8. Enter normal dummy names with the same character lengths in the game.",
                        "Surname=嘉嶋",
                        "GivenName=尚紀",
                        "Nickname=人修羅"
                    }, new UTF8Encoding(false));
                    LoggerInstance.Msg($"[NocturneKeyboardInput] Created config: {path}");
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    if (String.IsNullOrEmpty(value)) continue;
                    if (key.Equals("Surname", StringComparison.OrdinalIgnoreCase)) _surname = value;
                    else if (key.Equals("GivenName", StringComparison.OrdinalIgnoreCase)) _givenName = value;
                    else if (key.Equals("Nickname", StringComparison.OrdinalIgnoreCase)) _nickname = value;
                }
                LoggerInstance.Msg($"[NocturneKeyboardInput] Loaded config: {path}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[NocturneKeyboardInput] Config load failed; defaults will be used: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TryApplyConfiguredNames()
        {
            try
            {
                var global = dds3GlobalWork.DDS3_GBWK;
                if (global == null || global.name_code == null || global.name_nums == null ||
                    global.cname_code == null || global.cname_nums == null) return;
                if (global.name_code.Length == 0 || global.name_nums.Length == 0 ||
                    global.cname_code.Length == 0 || global.cname_nums.Length == 0) return;
                if (global.name_code[0] == null || global.name_nums[0] == null || global.cname_code[0] == null) return;

                string oldSurname = frName.frGetName1NString(0) ?? "";
                string oldGivenName = frName.frGetName2NString(0) ?? "";
                string oldNickname = frName.frGetCNameString(0) ?? "";
                if (!HasSameLength("Surname", oldSurname, _surname) ||
                    !HasSameLength("GivenName", oldGivenName, _givenName) ||
                    !HasSameLength("Nickname", oldNickname, _nickname))
                {
                    _finalNamesApplied = true;
                    LoggerInstance.Error("[NocturneKeyboardInput] Name replacement cancelled. Enter dummy names with exactly the same character lengths as the configured names.");
                    return;
                }

                Il2CppStructArray<byte> nameRow = global.name_code[0];
                Il2CppStructArray<byte> nameCountRow = global.name_nums[0];
                if (nameCountRow.Length < 2)
                {
                    _finalNamesApplied = true;
                    LoggerInstance.Error($"[NocturneKeyboardInput] Name count row is too short: {nameCountRow.Length}.");
                    return;
                }

                int surnameOffset = 0;
                int givenOffset = nameCountRow[0] * 2;
                if (nameCountRow[0] != oldSurname.Length || nameCountRow[1] != oldGivenName.Length ||
                    !MatchesUtf16(nameRow, surnameOffset, oldSurname) || !MatchesUtf16(nameRow, givenOffset, oldGivenName))
                {
                    _finalNamesApplied = true;
                    LoggerInstance.Error($"[NocturneKeyboardInput] Packed name layout validation failed. counts={nameCountRow[0]}/{nameCountRow[1]}, offsets={surnameOffset}/{givenOffset}.");
                    return;
                }

                WriteUtf16(nameRow, surnameOffset, _surname);
                WriteUtf16(nameRow, givenOffset, _givenName);
                nameCountRow[0] = checked((byte)_surname.Length);
                nameCountRow[1] = checked((byte)_givenName.Length);

                Il2CppStructArray<byte> nicknameRow = global.cname_code[0];
                if (nicknameRow.Length < _nickname.Length * 2)
                {
                    _finalNamesApplied = true;
                    LoggerInstance.Error($"[NocturneKeyboardInput] Nickname row is too short: {nicknameRow.Length}.");
                    return;
                }
                for (int i = 0; i < nicknameRow.Length; i++) nicknameRow[i] = 0xFF;
                WriteUtf16(nicknameRow, 0, _nickname);
                global.cname_nums[0] = checked((byte)_nickname.Length);

                string observedSurname = frName.frGetName1NString(0) ?? "";
                string observedGivenName = frName.frGetName2NString(0) ?? "";
                string observedNickname = frName.frGetCNameString(0) ?? "";
                _finalNamesApplied = true;
                LoggerInstance.Msg($"[NocturneKeyboardInput] Final names replaced. Surname=\"{observedSurname}\", GivenName=\"{observedGivenName}\", Nickname=\"{observedNickname}\"");
            }
            catch (Exception ex)
            {
                if (_postNameFrames >= 180)
                {
                    _finalNamesApplied = true;
                    LogException("Configured name replacement gave up after 180 frames", ex);
                }
            }
        }

        private bool HasSameLength(string label, string current, string configured)
        {
            if (current.Length == configured.Length) return true;
            LoggerInstance.Error($"[NocturneKeyboardInput] {label} length mismatch: dummy=\"{current}\" ({current.Length}), configured=\"{configured}\" ({configured.Length}).");
            return false;
        }

        private static int FindUtf16(Il2CppStructArray<byte> row, string value)
        {
            if (String.IsNullOrEmpty(value)) return -1;
            for (int offset = 0; offset <= row.Length - value.Length * 2; offset++)
            {
                bool match = true;
                for (int i = 0; i < value.Length; i++)
                {
                    ushort code = value[i];
                    if (row[offset + i * 2] != (byte)(code & 0xFF) || row[offset + i * 2 + 1] != (byte)(code >> 8))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return offset;
            }
            return -1;
        }

        private static bool MatchesUtf16(Il2CppStructArray<byte> row, int offset, string value)
        {
            if (offset < 0 || offset + value.Length * 2 > row.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                ushort code = value[i];
                if (row[offset + i * 2] != (byte)(code & 0xFF) || row[offset + i * 2 + 1] != (byte)(code >> 8)) return false;
            }
            return true;
        }

        private static void WriteUtf16(Il2CppStructArray<byte> row, int offset, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                ushort code = value[i];
                row[offset + i * 2] = (byte)(code & 0xFF);
                row[offset + i * 2 + 1] = (byte)(code >> 8);
            }
        }

        private static nmeData_t? TryGetNameWork()
        {
            try { return Nme_Update.GBWK; }
            catch { return null; }
        }

        private static sbyte TryGetNameProcess()
        {
            try { return Nme_Init.nmeChkNameProcess(); }
            catch { return 0; }
        }

        private void LogException(string context, Exception ex)
        {
            LoggerInstance.Error($"[NocturneKeyboardInput] {context}: {ex.GetType().FullName}: {ex.Message}\n{ex}");
        }
    }

    [HarmonyPatch(typeof(cmpMisc), nameof(cmpMisc.cmpChkCommonInput), new Type[] { typeof(uint) })]
    internal static class CommonInputPatch
    {
        private static int _pendingStart;
        private static int _pendingConfirm;

        internal static void RequestOnce() => Interlocked.Exchange(ref _pendingStart, 1);
        internal static void RequestConfirmPair() => Interlocked.Exchange(ref _pendingConfirm, 2);

        private static bool ConsumePendingConfirm()
        {
            while (true)
            {
                int current = Volatile.Read(ref _pendingConfirm);
                if (current <= 0) return false;
                if (Interlocked.CompareExchange(ref _pendingConfirm, current - 1, current) == current) return true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(uint ControlBit, ref uint __result)
        {
            if (ControlBit == 0x1000u && Interlocked.CompareExchange(ref _pendingStart, 0, 1) == 1)
            {
                __result |= 0x1000u;
                MelonLogger.Msg("[NocturneKeyboardInput][AUTO-START-INPUT] Injected one START bit through cmpChkCommonInput.");
                return;
            }

            if (NameConfirmScopePatch.IsActive && (ControlBit & 0x1u) != 0 && ConsumePendingConfirm())
            {
                // The confirmation caller treats this grouped common-input check as boolean.
                __result = 1u;
                MelonLogger.Msg($"[NocturneKeyboardInput][AUTO-YES] Injected one quiet scoped check through cmpChkCommonInput(0x{ControlBit:X}).");
            }
        }
    }

    [HarmonyPatch(typeof(Nme_Update), nameof(Nme_Update.nmeUpdateConfirm))]
    internal static class NameConfirmScopePatch
    {
        [ThreadStatic]
        private static bool _isActive;

        internal static bool IsActive => _isActive;

        [HarmonyPrefix]
        private static void Prefix() => _isActive = true;

        [HarmonyPostfix]
        private static void Postfix() => _isActive = false;

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            _isActive = false;
            return __exception;
        }
    }
}
