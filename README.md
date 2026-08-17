# SMT3HD Nocturne Keyboard Input

> 開発者向け: Steam Big Pictureのゲームパッド文字入力を安全に利用する実証済みブリッジは、[`experiments/SteamTextInputPoC`](experiments/SteamTextInputPoC/)にあります。日本語入力・全角スペース・連続起動・取消を実機確認済みです。

Steam版『真・女神転生III NOCTURNE HD REMASTER』の主人公名を、Windowsの日本語IMEで入力するMelonLoader MODです。

ゲーム本来の文字パレットには存在しない文字も、姓・名・通称の入力完了後にゲーム内の名前データへ反映します。初期値はドラマCD版の主人公名「嘉嶋 尚紀」、通称「人修羅」です。

## 対応環境

- Steam版 SMT3HD（日本語）
- Windows x64
- MelonLoader 0.6.1系
- .NET 6 Desktop Runtime（入力ウィンドウ用）

他言語版や異なるMelonLoader構成では未確認です。

## インストール

ReleaseのZIPをSMT3HDのゲームフォルダーへ展開してください。配置後は次の構成になります。

```text
smt3hd/
├─ Mods/
│  └─ NocturneKeyboardInput.dll
└─ UserData/
   └─ NocturneKeyboardInput/
      ├─ NameInputHelper.exe
      ├─ NameInputHelper.dll
      ├─ NameInputHelper.deps.json
      └─ NameInputHelper.runtimeconfig.json
```

## 使い方

1. ニューゲームを開始し、主人公の姓名入力画面まで進みます。
2. 文字パレットのカーソルを通常の文字に合わせた状態で `F8` を押します。
3. 表示された入力ウィンドウへ、完成形の姓・名・通称を入力します。
4. 「自動入力を開始」を押します。
5. MODが必要な長さのダミー文字を入力し、姓名、通称、最後の「はい」まで自動で確定します。
6. 確定後、ダミー文字列が入力ウィンドウで指定した名前へ置換されます。

文字数は姓・名がそれぞれ1～4文字、通称が1～8文字です。入力値は次回用として`UserData/NocturneKeyboardInput.cfg`へ保存されます。

## 注意事項

- 名前入力画面以外ではF8を使用しないでください。
- 初回は新規ゲームで動作確認することを推奨します。
- セーブデータを直接編集するMODではありませんが、MOD利用前には通常どおりバックアップを推奨します。
- ゲーム本体、ATLUS、SEGAのファイルはこのリポジトリに含まれません。

## ソースからのビルド

既定では次のSteam標準インストール先を参照します。

```text
C:\Program Files (x86)\Steam\steamapps\common\smt3hd
```

別の場所へインストールしている場合は`GameDir`を指定してください。

```powershell
dotnet build .\src\NocturneKeyboardInput.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\smt3hd"
dotnet build .\src\NameInputHelper\NameInputHelper.csproj -c Release
```

配布用フォルダーは次のコマンドで生成できます。

```powershell
.\build-release.ps1
```

## 技術概要

入力ウィンドウで完成文字列を取得し、ゲーム本来の名前入力処理で同じ文字数のダミー文字を確定させた後、UTF-16の名前領域を検証して置換します。画面遷移にはゲーム既存の入力処理を使用し、最後の確認入力も受付可能になるまで待ってから送信します。

調査経緯は[SMT3HD_KEYBOARD_INPUT_DISCOVERY_REPORT_JA.md](SMT3HD_KEYBOARD_INPUT_DISCOVERY_REPORT_JA.md)を参照してください。

## ライセンス

[MIT License](LICENSE)

本プロジェクトは非公式のファン制作MODであり、ATLUSおよびSEGAとは関係ありません。
