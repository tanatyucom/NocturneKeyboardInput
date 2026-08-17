# Steam Text Input PoC

SMT3HDに同梱されたSteamworks IL2CPPラッパーを利用し、Steamのゲームパッド文字入力UIから完成文字列を取得する独立PoC兼再利用ブリッジです。

実機で通常Steam時の安全な拒否、Big Pictureでの表示、日本語・全角スペースの取得、連続起動、取消を確認済みです。

## 安全範囲

- `Nme_Update`、`ImeKeyboard`、`NameSet()`を呼びません。
- ゲームの名前データやUI状態を書き換えません。
- 取得文字列はMelonLoaderログへ出力するだけです。
- 正式版`NocturneKeyboardInput.dll`とは別DLLです。

## 再利用クラス

`Smt3SteamTextInputBridge.cs`は、SMT3HD向けMelonLoader MODから利用できる公開クラスです。

```csharp
var bridge = new Smt3SteamTextInputBridge();

bridge.TryOpen(
    new SteamTextInputOptions
    {
        Description = "名前を入力",
        MaximumCharacters = 8
    },
    result => MelonLogger.Msg(result.Text),
    () => MelonLogger.Msg("Canceled"),
    error => MelonLogger.Error(error),
    out string failureReason);
```

所有するMODは終了時に`Dispose()`を呼んでください。同時に開ける要求は1件だけです。

Steamコールバックは全登録先へ配信されますが、このブリッジは自分が開いた要求の処理中だけ結果を取得します。ゲーム本体や別MODが開いた入力UIは無視します。

## 操作

1. Steam Big PictureからSMT3HDを起動します。
2. ゲーム中にF9を押します。
3. Steam入力UIで文字列を入力し、決定または取消します。
4. `MelonLoader/Latest.log`で`SteamTextInputPoC`を検索します。

通常のSteamデスクトップ起動では、Steam公式仕様上`ShowGamepadTextInput`が`false`を返す可能性があります。これはPoCの失敗ではなく、表示条件を確認できた正常な結果です。

## 期待ログ

```text
[SteamTextInputPoC] OPENED BigPicture=True MaxCharacters=32
[SteamTextInputPoC] SUBMITTED Utf16Units=3 BufferBytesIncludingNull=10 Text="人修羅"
```

取消時:

```text
[SteamTextInputPoC] CANCELED
```

## 対応範囲

- SMT3HD 1.0.4
- MelonLoader 0.6.1 Open-Beta
- Windows版Steam
- Steam Big Pictureで実機確認済み
- 通常デスクトップ起動ではSteam APIが入力UIを提供しない

このソースはSMT3HD同梱の`Il2CppSteamworks`型を参照します。他ゲームへ移植する場合は、そのゲームのSteamworksラッパーまたはSteamworksネイティブAPI用アダプターが必要です。

## ライセンス

リポジトリルートのMIT Licenseが適用されます。再利用・改変・再配布時は著作権表示とライセンス文を保持してください。
