using System;
using Il2Cpp;
using Il2CppSteamworks;

namespace SteamTextInputPoC
{
    public sealed class SteamTextInputOptions
    {
        public string Description { get; set; } = "Text Input";
        public string ExistingText { get; set; } = "";
        public uint MaximumCharacters { get; set; } = 32;
        public bool MultipleLines { get; set; }
        public bool Password { get; set; }
    }

    public sealed class SteamTextInputResult
    {
        public SteamTextInputResult(string text, uint bufferBytesIncludingNull)
        {
            Text = text ?? "";
            BufferBytesIncludingNull = bufferBytesIncludingNull;
        }

        public string Text { get; }
        public uint BufferBytesIncludingNull { get; }
    }

    /// <summary>
    /// Reusable SMT3HD adapter for Steam's Big Picture gamepad text input.
    /// Owns callback lifetime, prevents overlapping requests, and never touches
    /// SMT3 game data.
    /// </summary>
    public sealed class Smt3SteamTextInputBridge : IDisposable
    {
        private const uint MaximumSafetyBufferBytes = 64 * 1024;

        private Callback<GamepadTextInputDismissed_t>? _callback;
        private Callback<GamepadTextInputDismissed_t>.DispatchDelegate? _dispatch;
        private Action<SteamTextInputResult>? _onSubmitted;
        private Action? _onCanceled;
        private Action<string>? _onFailed;
        private bool _disposed;

        public bool IsRequestActive { get; private set; }

        public bool TryOpen(
            SteamTextInputOptions options,
            Action<SteamTextInputResult> onSubmitted,
            Action onCanceled,
            Action<string> onFailed,
            out string failureReason)
        {
            failureReason = "";

            if (_disposed)
                return FailOpen("The bridge has already been disposed.", out failureReason);
            if (IsRequestActive)
                return FailOpen("A Steam text input request is already active.", out failureReason);
            if (options == null)
                return FailOpen("Options must not be null.", out failureReason);
            if (options.MaximumCharacters == 0)
                return FailOpen("MaximumCharacters must be greater than zero.", out failureReason);
            if (onSubmitted == null || onCanceled == null || onFailed == null)
                return FailOpen("All result callbacks must be supplied.", out failureReason);

            try
            {
                if (!SteamManager.Initialized)
                    return FailOpen("SteamManager is not initialized.", out failureReason);

                EnsureCallbackRegistered();
                _onSubmitted = onSubmitted;
                _onCanceled = onCanceled;
                _onFailed = onFailed;

                bool opened = SteamUtils.ShowGamepadTextInput(
                    options.Password
                        ? EGamepadTextInputMode.k_EGamepadTextInputModePassword
                        : EGamepadTextInputMode.k_EGamepadTextInputModeNormal,
                    options.MultipleLines
                        ? EGamepadTextInputLineMode.k_EGamepadTextInputLineModeMultipleLines
                        : EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine,
                    options.Description ?? "",
                    options.MaximumCharacters,
                    options.ExistingText ?? "");

                if (!opened)
                {
                    ClearRequestCallbacks();
                    return FailOpen(
                        "ShowGamepadTextInput returned false. Big Picture text input is unavailable.",
                        out failureReason);
                }

                IsRequestActive = true;
                return true;
            }
            catch (Exception ex)
            {
                ClearRequestCallbacks();
                return FailOpen($"{ex.GetType().FullName}: {ex.Message}", out failureReason);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            IsRequestActive = false;
            ClearRequestCallbacks();
            try { _callback?.Dispose(); }
            finally
            {
                _callback = null;
                _dispatch = null;
            }
        }

        private void EnsureCallbackRegistered()
        {
            if (_callback != null)
                return;

            _dispatch = (Action<GamepadTextInputDismissed_t>)OnDismissed;
            _callback = Callback<GamepadTextInputDismissed_t>.Create(_dispatch);
            if (_callback == null)
                throw new InvalidOperationException("Steam callback registration returned null.");
        }

        private void OnDismissed(GamepadTextInputDismissed_t callback)
        {
            // Steam broadcasts this callback to every registered listener.
            // Ignore dialogs opened by the game or another mod unless this
            // bridge currently owns an active request.
            if (!IsRequestActive)
                return;

            Action<SteamTextInputResult>? submitted = _onSubmitted;
            Action? canceled = _onCanceled;
            Action<string>? failed = _onFailed;
            IsRequestActive = false;
            ClearRequestCallbacks();

            try
            {
                if (!callback.m_bSubmitted)
                {
                    canceled?.Invoke();
                    return;
                }

                // This Steamworks build includes the terminating NUL byte in
                // the returned length (verified with Japanese UTF-8 input).
                uint bufferBytesIncludingNull = SteamUtils.GetEnteredGamepadTextLength();
                if (bufferBytesIncludingNull == 0 ||
                    bufferBytesIncludingNull > MaximumSafetyBufferBytes)
                {
                    failed?.Invoke("Steam returned an invalid input buffer length: " + bufferBytesIncludingNull);
                    return;
                }

                if (!SteamUtils.GetEnteredGamepadTextInput(
                        out string text,
                        bufferBytesIncludingNull))
                {
                    failed?.Invoke(
                        "GetEnteredGamepadTextInput returned false. " +
                        $"BufferBytesIncludingNull={bufferBytesIncludingNull}");
                    return;
                }

                submitted?.Invoke(new SteamTextInputResult(text ?? "", bufferBytesIncludingNull));
            }
            catch (Exception ex)
            {
                failed?.Invoke($"{ex.GetType().FullName}: {ex.Message}");
            }
        }

        private void ClearRequestCallbacks()
        {
            _onSubmitted = null;
            _onCanceled = null;
            _onFailed = null;
        }

        private static bool FailOpen(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }
}
