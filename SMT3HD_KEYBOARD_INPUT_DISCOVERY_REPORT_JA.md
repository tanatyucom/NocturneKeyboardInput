# SMT3HD Keyboard Direct Input MOD — 技術調査報告

調査日: 2026-08-16

対象: Steam版 Shin Megami Tensei III Nocturne HD Remaster 1.0.4

調査方式: 読み取り専用の静的調査（ゲーム実行中の注入・セーブ試験は未実施）

## 結論

名前入力は汎用 `UnityEngine.UI.InputField` ではなく、ゲーム固有の `Nme_*` 系で実装されている。ただしPC版には、完成文字列を受け取る独自のキーボード/IME経路も既に存在する。

最有力のPoCは、呼び名画面で `Nme_KeyBoard.MojiSave(string name)` に `"人修羅"` を渡し、既存の確定処理へ戻す方法である。通常のパレット操作を残したまま並行利用できる可能性が高い。

一方、最終保存表現は単純な `System.String` だけではない。UI・IME側は `System.String` / TextMeshProを使うが、ゲーム本体のグローバルワークは名前を文字数配列とコード配列として保持する。この変換と、セーブ後の復元はランタイムPoCで確認する必要がある。

## A. 環境

- 実行形式: Unity IL2CPP、x64
- Unity: 2019.4.10f1
- ゲーム: 1.0.4
- MODローダー: MelonLoader 0.6.1 Open-Beta、net6
- フック基盤: Harmony / Il2CppInterop
- 主な生成済み参照:
  - `MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll`
  - `Il2Cppmscorlib.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.InputLegacyModule.dll`
  - `UnityEngine.UI.dll`
  - `Unity.TextMeshPro.dll`
- 既存 `NocturneDetailedSkillInfo` は `net6.0` のMelonModで、上記DLLを直接参照し、`HarmonyInstance.PatchAll` を使用する。将来のPoCは同構成を独立プロジェクトとして再利用できる。
- 指定先 `C:\SMT3Modding\NocturneKeyboardInput` は存在するが空だった。本調査では変更していない。
- `NocturneDetailedSkillInfo` その他の既存MOD、ゲーム本体、セーブデータはいずれも変更していない。

## B. 名前入力アーキテクチャ

### 中核クラス

`Il2Cpp.Nme_Init`

- `nmeNameProcessStart(sbyte TargetNo, sbyte EntryMode, sbyte DebugMode)`
- `nmeNameEntry()` / `nmeChkNameProcess()` / `nmeNameProcessEnd()`
- `nmeInitEntryType(sbyte EntryType, ref nmeData_t)`
- `nmeGetInputCharMaxCore(int Type)` / `nmeGetInputCharMax(ref nmeData_t)`
- `nmeSetDefaultName*`

`Il2Cpp.Nme_Update`

- `nmeGetInputChar()` — パレット文字の取り込み候補
- `nmeDeleteCharCursorPos(sbyte Mode)` / `nmeBackSpace()` — 削除
- `nmeMoveInputCursor(sbyte Dir)` — カーソル移動
- `nmeUpdateConfirm()` — 確認
- `nmeChkInvalidName(sbyte Type)` / `nmeCompareStr(sbyte Type)` — 検証
- `NameSet()` — 一時入力からゲーム側データへの反映候補
- `KeybordInit()` / `KeybordDateInit()` — PCキーボード経路の初期化

`Il2Cpp.Nme_Draw`（日本語系）および言語別の `Nme_Draw_EFGIS`, `Nme_Draw_KOREANA`, `Nme_Draw_TCHINESE`

- `InputName()` または `NameInput()`
- `NameFuncInput()` / `NameFuncDalate()`（言語別実装）
- `NameInputEnd()` / `nmeDrawInputConfirm()`
- 一時表示: `nameTmpTextTP`, `cnameTmpTextTP`, `nameTMPStr[]`, `cnameTMPStr[]`
- 最終確認表示: `FullNameTextTM`, `NickNameTextTM`
- UI: `name_list`, `name_input`, delete/endボタン、ひらがな・カタカナ・英字・漢字リスト
- 長さ関連: `nameMax`, `CnameMax`, `nameEnglishMax`, `nameEntryTypeMax`

`Il2Cppname_H.nmeData_t`

- `TargetNo`, `EntryMode`, `EntryType`, `CfmFlag`
- `InputNums : sbyte[]`
- `InputPos : sbyte`
- `InputCode : Il2CppObjectBase`
- 各種カーソル情報

この構造から、姓・名・呼び名は完全に別の画面クラスではなく、`TargetNo` / `EntryMode` / `EntryType` により同じ名前入力機構を切り替えている可能性が高い。UIには姓/名用 `Sei*`, `Mei*` と、フルネーム/呼び名用 `FullName*`, `NickName*` が別々に存在する。

### データフロー（静的推定）

1. `Nme_Init.nmeNameProcessStart` が対象と入力モードを決定する。
2. `nmeData_t.InputNums`, `InputPos`, `InputCode` が編集中のコード列を保持する。
3. パレットでは `Nme_Update.nmeGetInputChar`、PC経路では後述の `ImeKeyboard.InputText` が入力源になる。
4. 表示用には `System.String` と `TextMeshProUGUI` が使われる。
5. `nmeChkInvalidName` と最大文字数処理を経て `nmeUpdateConfirm` / `NameSet` がゲーム側データへ反映する。
6. `frName.frGetNameString`, `frGetName1NString`, `frGetName2NString`, `frGetCNameString` が後続画面向け文字列を返す。

ステップ3～5の正確な呼出順はIL2CPPネイティブ本体内にあり、生成DLLのシグネチャだけでは確定できない。

## C. 文字列表現

結論: **ハイブリッド表現**。

- UI/IME層: `System.String`（IL2CPP上のUTF-16文字列）
  - `ImeKeyboard.InputText : string`
  - `Nme_KeyBoard.UseName : string`
  - `Nme_KeyBoard.MojiSave(string)`
  - TextMeshProの表示フィールド
- 通常名前入力の一時層:
  - `nmeData_t.InputNums : sbyte[]`
  - `nmeData_t.InputCode : object`（言語により実体が変わる可能性）
- ゲームグローバルワーク/保存元:
  - `dds3GlobalWork_t.name_nums : byte[][]`
  - `dds3GlobalWork_t.name_code : byte[][]`
  - `dds3GlobalWork_t.cname_nums : byte[]`
  - `dds3GlobalWork_t.cname_code : byte[][]`
  - `dds3GlobalWork_t.nameLanguage : int`
- 別のゲームデータ構造には `datUnitWork_s.namecode/fullnamecode : char[]` がある。
- セーブ見出し用と思われる `GameFileInfo_t.cname_code` は `string` である。

したがって、「名前はセーブまで常に.NET文字列」とは言えない。最有力の変換点は `Nme_KeyBoard.MojiSave(string)` または `Nme_Update.NameSet()` であり、ここでUnicode文字列が内部コード列へ変換されると考えられる。

## D. `人修羅` の実現可能性

評価: **LIKELY（高いが未確認）**

- 表現: UI/IME層の `System.String` は `人修羅` を表現できる。
- グリフ: `人修羅` のUTF-8文字列が実際の日本語ゲームデータ（例: `StreamingAssets/PC/common_ja`, `battle_ja`、複数の日本語イベント/DLCデータ）に存在する。少なくとも通常メッセージ用日本語フォントに必要グリフがある可能性は非常に高い。
- UI: 名前画面はTextMeshProを使用し、既存の日本語漢字リストも持つ。Unicode文字列表示の基盤はある。
- 保存: 内部コード配列が `人`, `修`, `羅` のすべてを受理・再符号化できるかは未確認。
- 再読込: 未試験。

なお、ゲームデータ内に語が存在することは「メッセージフォントにグリフがある」強い証拠だが、「名前用コード表に3文字すべてが登録済み」の証明ではない。そのためCONFIRMEDとはしない。

## E. 推奨フック

### 第一候補

呼び名入力中にデバッグキーを検出し、既存の公開メソッドを利用する。

```text
Il2Cpp.Nme_KeyBoard.MojiSave(System.String name)
```

`"人修羅"` を渡した後、ゲーム既存の確定UIを使う。このメソッドは、文字列から既存の名前データへ渡すために用意された入口として最も強い候補である。

呼び名画面判定には、以下を組み合わせて誤作動を防ぐ。

- `Nme_KeyBoard.GBWK.TargetNo / EntryMode / EntryType`
- `Nme_Update.GBWK` の同フィールド
- `Nme_Init.nmeChkNameProcess()` または `Nme_KeyBoard.nmeChkKeyBoardProcess()`
- 必要なら `NickNameTextTM` / 名前UIの生存確認

### 第二候補

`ImeKeyboard.InputCallbackOK(string text)` の直後、または `Nme_Update.NameSet()` の直前に完成文字列を差し替える。フルIME経路の動作をそのまま利用できる反面、他のテキスト用途と共有される可能性があるため厳密な画面ガードが必要。

### 避けるべき初手

- `dds3GlobalWork_t.*_code` の直接書換え
- セーブファイルの後処理
- TextMeshProの表示文字列だけの書換え

これらは、表示と実データの不一致や変換規則の取りこぼしを起こしやすい。

## F. キーボード / IME

- ASCII入力: **高い実現性**。独自PC経路と `MojiSave(string)` があり、完成文字列を渡せる。
- 日本語の直接Unicode挿入: **高い実現性**。同じ `string` 入口を使用できる。ただし内部コード変換の受理範囲は要試験。
- 日本語IME変換: **実装自体は既に存在する**。
  - `ImeKeyboard.OpenIME(titleText, initText, maxLength, lang)`
  - `InputCallbackOK/Cancel/Abort(string)`
  - `SteamIME.OpenIME(..., maxLength, lang, posx, posy)`
  - Steamworksの `GamepadTextInputDismissed_t`

  ただし、これはWindows標準のインラインIME合成というよりSteamのテキスト入力UIを介する実装に見える。実際に日本語Windows IMEの「にんしゅら→人修羅」が通るかはランタイム確認が必要。
- クリップボード貼付: ゲーム固有の貼付APIは静的調査では見つからなかった。MOD側でクリップボードから完成済みUnicode文字列を取り、`MojiSave` に渡す方式なら実現可能性が高い。
- Backspace: 既存 `Nme_Update.nmeBackSpace()` があるため、パレット/コントローラ機能を維持できる。
- Enter: 既存確定経路は `nmeUpdateConfirm()`。直接呼出す前に状態機械の条件確認が必要で、初期PoCではゲーム側の通常確定操作を使う方が安全。

## G. 最小PoC案

1. 独立したnet6 MelonModを `C:\SMT3Modding\NocturneKeyboardInput` に作る。
2. 毎フレームF8を監視する（ゲーム全体ではなく名前入力プロセス中だけ）。
3. `TargetNo/EntryMode/EntryType` と名前UI状態をログ出力し、呼び名画面の値を一度特定する。
4. 呼び名画面でのみ `Nme_KeyBoard.MojiSave("人修羅")` を呼ぶ。
5. 表示が更新されたことを確認し、ゲームパッドの通常操作で確定する。
6. 新規セーブを作成し、タイトルへ戻ってロード後も表示されるか確認する。

このPoCで、`Unicode string → 既存変換 → 名前バッファ → 表示 → 確定 → セーブ → 再読込` を最小範囲で検証できる。

もし `MojiSave` が現在画面の一時バッファを更新しない場合のみ、`ImeKeyboard.InputCallbackOK("人修羅")`、次に `Nme_Update.NameSet()` 前の差替えを順に試す。

## H. リスク / 未確定事項

- `TargetNo`, `EntryMode`, `EntryType` の姓・名・呼び名への正確な対応値
- `Nme_KeyBoard.MojiSave` が日本語UIで使用されるか、PC専用別経路か
- 日本語版 `Nme_Draw` と `Nme_KeyBoard` の間の実際の呼出順
- `InputCode` の実体型と文字列からコード配列への変換規則
- 最大長がUTF-16コード単位、Unicodeスカラー、内部グリフ数のどれで数えられるか
- `nmeChkInvalidName` がパレット外文字を拒否するか
- `人`, `修`, `羅` が名前用コード表にすべて存在するか
- 名前画面のTextMeshProフォントアセットが通常メッセージと同じグリフを持つか
- セーブとロードで `nameLanguage` を含め正しく復元されるか
- SteamIMEがWindows日本語IME変換を実際に提供するか
- 他MODとのF8競合および入力フォーカス競合

## 最終推奨

次のタスクは **1. `F8 → 人修羅` 注入PoCを作る** が妥当。

追加の広範な静的解析より、公開された `MojiSave(string)` を名前入力状態のログ付きで試す方が、保存コード変換、表示、検証、セーブ復元という残る重要点を最短で確定できる。PoCでは自動確定せず、ゲームの通常確定操作を使うことを推奨する。

## 追補: 2026-08-17 実機PoC後の解析

### 直接バッファ注入の結果

`Nme_Update.GBWK.InputCode` へUTF-16LEを直接書くと、文字数とカーソル位置およびBackspaceは反映されたが、名前入力UI上では空白になった。`UnityEngine.Input.inputString` は日本語IMEの確定文字列ではなく `k`, `a` などのローマ字キーを返した。したがって、この経路を実用入力には使用しない。

### 最終グローバルデータ置換の結果

通常の名前入力処理を完了した後、`dds3GlobalWork.DDS3_GBWK` の `name_code[0]` と `cname_code[0]` をUTF-16LEで置換する方式は実機で表示確認済みである。苗字 `嘉嶋`、名前 `尚紀`、通称 `人修羅` が正常表示された。実用版はUTF-8設定ファイルを読み、同じ文字数のダミー名から安全に置換する。

### ゲーム既存のSteam文字入力経路

`Nme_Update` には次の静的状態とメソッドが存在する。

- `ImeKeyboard keyboardObj_a`
- `keyboardUseflag`, `KeyBordflag`, `MojiInputflag`
- `KeybordInit()`, `KeybordDateInit()`, `NameSet()`

`ImeKeyboard` は `OpenIME(titleText, initText, maxLength, lang)`、`InputText`、`IsOpenIME`、`IsCancel` と完了・取消コールバックを持つ。その下層の `SteamIME.OpenIME(...)` はSteamのゲームパッド文字入力を開き、`GamepadTextInputDismissed_t` のコールバックで完成文字列を受け取る構造である。

別系統の `Nme_KeyBoard` にも `MojiInput()`、`MojiSave(string)`、`nmeChack(string, bool)`、`nmeKeyBoardProcessStart(TargetNo, DebugMode)` が存在する。IL2CPP生成ラッパー上では呼び出し可能である。

以上から、リアルタイムの生キー注入ではなく、ゲーム既存のSteam文字入力オーバーレイを正規状態で開き、完成文字列を既存の `NameSet()` または `MojiSave(string)` に処理させる経路が次の最有力候補である。次回PoCは書き込みをせず、名前画面中の `keyboardObj_a` の生存、各フラグ、`Lang`、`EntryType` の遷移をログ観測する。

### Steam入力経路の追加解析と禁止事項

実機観測では日本語パレット画面中の `Nme_Update.keyboardObj` と `keyboardObj_a` はともに `null`、関連フラグもすべて無効だった。`EntryType=0` が苗字・名前工程、`EntryType=2` が通称工程である。

`KeybordInit()` を単独で呼ぶPoCでは両オブジェクトが生成されたが、ゲーム全体のUIが消え、後続イベントが進行不能になった。ログでは同時に `Lang: 0 -> 1`、`Num: 0 -> 4` が観測された。このPoCは撤去済みであり、`KeybordInit()` の直接呼び出しを禁止する。

Cpp2ILとGameAssemblyの静的解析により次を確認した。

- `KeybordInit()` RVA `0x2669C90`: 冒頭で `KeybordDateInit()` を呼び、その後 `ImeKeyboard.OpenIME()` を直接呼ぶ。単なる生成関数ではない。
- `KeybordDateInit()` RVA `0x26696A0`: 名前UI配下のオブジェクトを検索し、`keyboardObj` と `keyboardObj_a` を設定した後、言語・最大長・初期文字列・ヘルプ文字列を準備する。
- `NameSet()` RVA `0x266A090`: Steam入力完了後の名前反映候補。
- `Nme_Update.nmeUpdate()` RVA `0x2673420`: `SDF_PADMAP_OPT1` のトリガーを確認し、`KeybordDateInit()`、`ImeKeyboard.OpenIME()` を正規分岐内で呼ぶ。`KeyBordflag` が立つと `NameSet()` を呼ぶ。
- `ImeKeyboard.OpenIME()` RVA `0x270DFD0` には、日本語パレット側と外国語キーボード側を合わせて4つの直接呼び出し元がある。

次の候補は `KeybordDateInit()` のみを使って参照とパラメータを準備し、正規のOPT1分岐へ渡す方式だが、実機呼び出し前にUI復帰条件と `KeyBordflag` の設定元をさらに静的解析する。

### Steam入力経路の到達不能性

追加のネイティブ命令解析で静的フィールド配置を確定した。

- `+0x50`: `MojiInputflag`
- `+0x51`: `TabChageflag`
- `+0x52`: `KanjiScrollflag`
- `+0x53`: `FuncInputflag`
- `+0x54`: `CancelInputflag`
- `+0x55`: `keyboardUseflag`
- `+0x58`: `keyboardObj`
- `+0x60`: `keyboardObj_a`
- `+0x68`: `tblInvalidName`
- `+0x70`: `GBWK`
- `+0x78`: `KeyBordflag`
- `+0x7C`: `Num`
- `+0x80`: `Lang`
- `+0x88`: `Name`
- `+0x90`: `Help`

`nmeUpdate()` はOPT1トリガー時に `KeyBordflag` を一時的に1にして `KeybordDateInit()` と `ImeKeyboard.OpenIME()` を呼び、直後に0へ戻す。別途 `keyboardUseflag` が1なら `NameSet()` を呼ぶが、GameAssembly全体のクラス参照を追跡した結果、`keyboardUseflag` は静的初期化と `NameSet()` 終了時に0へ書かれるだけで、1へ設定する命令が見つからない。

`ImeKeyboard.OpenIME()` は `IsOpenIME=1`、`IsCancel=0` を設定してSteam APIへ渡す。Steamの `OnGamepadTextInputDissmissed()` は成功時に `InputCallbackOK(text)`、失敗時に `InputCallbackCancel()` を呼ぶ。しかし成功コールバックは `InputText` を保存するだけ、取消コールバックは `IsCancel=1` にするだけである。`IsOpenIME=0` にする `CloseIMECallback()` にはGameAssembly内からの直接呼び出し元が存在しない。

したがって日本語パレット側のSteam入力経路は、オブジェクト未生成に加えて完了通知から `NameSet()` へ到達する配線も欠けている。製品版で実際には使われていない未完成コードまたは移植残骸である可能性が非常に高い。これをMODから復活させるには、オブジェクト生成、UI退避・復帰、Steam完了監視、文字列検証、`NameSet()` 呼び出しをすべて補完する必要があり、ゲーム既存経路の単純な再利用とはならない。

安全性と実装規模から、このネイティブSteam経路を使うPoCは打ち切る。自由入力を追加する場合は、MOD独自の設定ファイル、クリップボード、または独自入力UIで完成文字列を取得し、名前入力完了後の最終グローバルデータへ反映する方式を採用する。

### 独自入力UI＋正規ダミー入力方式の静的解析

独自入力UIで完成文字列を保持し、その文字数に対応するダミー文字だけをゲーム本来の名前入力処理へ渡す方式を調査した。

- `Nme_Update.nmeGetInputChar()` RVA `0x266C0E0` は、現在の文字パレットとカーソル座標から選択文字を取得し、入力バッファ、文字数、表示、末尾空白処理などをまとめて更新する共通処理である。
- `Nme_Update.nmeUpdateInputCursor()` RVA `0x2671B90` 内からの直接呼び出しは `0x2672081`、`0x2672153`、`0x2672224`、`0x26722FE` の4か所で、いずれも入力デバイス・UI状態ごとの決定操作分岐である。
- `nmeUpdateInputCursor()` は入力マップ取得関数を `0xF3` で呼び、返されたビットマスクの `1` を決定操作として処理する。各UI経路固有の押下・多重入力防止状態を確認した後、最終的には同じ `nmeGetInputChar()` を呼ぶ。
- `nmeGetInputChar()` は `GBWK.CharCursor` の現在位置を読み、モードに応じて `y * 15 + x` または `y * 16 + x` でパレット表を参照する。したがって、カーソルが有効な通常文字上にある状態で呼べば、同じダミー文字を繰り返し入力できる可能性が高い。
- 以前失敗した `InputCode` への直接書込みと異なり、この方式はゲーム本来の選択処理を通るため、内部文字コード、入力数、表示用配列、カーソル、効果音・末尾処理の整合性を維持できる見込みがある。

実装PoCでは一度に複数回呼ばず、名前画面の有効状態を毎フレーム確認しながら1フレームにつき1文字だけ入力する。苗字・名前・通称の各工程は `EntryType` だけに依存せず、`TargetNo`、`EntryMode`、`InputNums` と工程遷移を併用して識別する。入力前に現在のパレット位置が通常文字であることも確認し、無効セル・機能セルなら停止する。

最終置換は既に実機確認済みの方式を維持する。独自UIに保持した苗字・名前・通称と、正規処理で入力された同文字数のダミー領域を名前入力工程終了後に置換する。これにより、ユーザーは独自UIへ実名を入力するだけで、ゲーム側の手動ダミー入力を意識しない操作を目指せる。
