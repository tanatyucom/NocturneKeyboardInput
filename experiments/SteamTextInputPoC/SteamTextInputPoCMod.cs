using System;
using Il2CppSteamworks;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(
    typeof(SteamTextInputPoC.SteamTextInputPoCMod),
    "Steam Text Input PoC",
    "0.2.0",
    "Gray Ghost")]
[assembly: MelonGame(null, "smt3hd")]

namespace SteamTextInputPoC
{
    /// <summary>
    /// F9-driven demonstration client for Smt3SteamTextInputBridge.
    /// Submitted text is logged only and never written to game data.
    /// </summary>
    public sealed class SteamTextInputPoCMod : MelonMod
    {
        private readonly Smt3SteamTextInputBridge _bridge =
            new Smt3SteamTextInputBridge();

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("[SteamTextInputPoC] Loaded reusable bridge demo v0.2.0. Press F9 to open.");
            LoggerInstance.Msg("[SteamTextInputPoC] Submitted text is logged only; game data is never modified.");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.F9))
                return;

            if (_bridge.IsRequestActive)
            {
                LoggerInstance.Warning("[SteamTextInputPoC] A request is already active; F9 was ignored.");
                return;
            }

            bool bigPicture = false;
            try
            {
                bigPicture = SteamUtils.IsSteamInBigPictureMode();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning(
                    "[SteamTextInputPoC] Big Picture state query failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }

            var options = new SteamTextInputOptions
            {
                Description = "Steam Text Input Test",
                ExistingText = "",
                MaximumCharacters = 32,
                MultipleLines = false,
                Password = false
            };

            bool opened = _bridge.TryOpen(
                options,
                result => LoggerInstance.Msg(
                    "[SteamTextInputPoC] SUBMITTED " +
                    $"Utf16Units={result.Text.Length} " +
                    $"BufferBytesIncludingNull={result.BufferBytesIncludingNull} " +
                    $"Text=\"{OneLine(result.Text)}\""),
                () => LoggerInstance.Msg("[SteamTextInputPoC] CANCELED"),
                error => LoggerInstance.Error(
                    "[SteamTextInputPoC] CALLBACK_FAIL " + error),
                out string failureReason);

            if (!opened)
            {
                LoggerInstance.Warning(
                    "[SteamTextInputPoC] OPEN_REJECTED " +
                    $"BigPicture={bigPicture} Reason=\"{OneLine(failureReason)}\"");
                return;
            }

            LoggerInstance.Msg(
                "[SteamTextInputPoC] OPENED " +
                $"BigPicture={bigPicture} MaxCharacters={options.MaximumCharacters}");
        }

        public override void OnDeinitializeMelon()
        {
            _bridge.Dispose();
        }

        private static string OneLine(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\"", "\\\"");
        }
    }
}
