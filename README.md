
# OpenUtau Xen

**[English](#english)** | **[日本語](#japanese)**

<a id="english"></a>

## English

OpenUtau Xen is a local derivative of [OpenUtau](https://github.com/stakira/OpenUtau)
for microtonal and multi-tuning singing synthesis. It is initially based on upstream
commit `3eb9dd26459fa51dc2c5bf46693a9492d817c130`. This is a development build; the
full first-release acceptance plan is tracked in
[`docs/microtonal/PLAN.md`](docs/microtonal/PLAN.md).

Download the latest version for Windows, macOS or Linux from
[Releases](https://github.com/palf-gh/OpenUtau-Xen/releases/latest). Every version
is released there; what each one contains is recorded in
[`PRODUCTION.md`](docs/microtonal/PRODUCTION.md).

### Editing and files

- Use **Project → Project tuning** for the project default, or the piano-roll
  tuning menu for a track override. References, including the A4 frequency, are editable.
- **Apply tuning** snaps existing notes to the new tuning. **Change reference**
  keeps degrees. **Apply display** changes only the active scale. The option
  to retain existing frequencies converts notes into a degree plus residual cents.
- `.ustxen` stores complete projects with embedded definitions. `.tunxen` stores
  reusable tuning, scale, notation and keyboard presets.
- Import upstream `.ustx`, Scala `.scl` and `.kbm`. Export compatible USTX as a
  separate file. MIDI export quantises to semitones; UST carries converted pitch
  offsets in its curves. Interchange formats cannot preserve all Xen metadata.
- The piano roll's **View** menu sets the row layout (all pitches, highlighted scale
  or compact scale) and the primary and secondary key labels. These are editor
  settings and do not change the tuning or the project. Every layout retains continuous
  physical pitch; the compact layout adds rows for existing off-scale notes.
- Key labels default to Stein–Zimmermann–Gould where a verified table exists (12, 24,
  31 and 53-EDO and the twelve-note presets), Bohlen–Pierce Lambda names (C D E F G H J
  A B) for equal divisions of the tritave, and Ups and Downs for other equal divisions.
- Keys have three colours: white for naturals, black for sharps and flats, and blue
  for the microtones between them, following the 31-tone Fokker organ. The layout is
  derived from each tuning's own generator and scales with the number of divisions
  ([keyboard specification](docs/microtonal_keyboard_algorithm_spec.md)).
- The note property **Tone** field accepts a natural plus or minus tuning steps
  (53-EDO: `C4`, `C4+1` … `C4+4`, `D4-4` … `D4-1`, `D4`), spelled names such as
  `C#4` or `Db4`, and relative steps such as `+3`.
- Tuning and MIDI controls, warnings and preset labels are localised in all
  20 registered languages. The source locale identifier en-US uses British English.

### Build and tests

Requires .NET 10 and the existing upstream native runtime dependencies.

```powershell
dotnet build OpenUtau
dotnet test OpenUtau.Test
dotnet publish OpenUtau -c Release -r win-x64 --self-contained true
```

External voice tests are opt-in and need the documented local voicebanks. Run them
serially, after the ordinary tests:

```powershell
pwsh -NoProfile -File tools/verify-xen-voices.ps1 -ClassicBank '<UTAU voicebank>' -DiffSingerBank '<DiffSinger configs>' -DiffSingerVocoder '<vocoder.onnx>' -UtauEngine '<folder with resampler.exe>'
```

The synthesis smoke checks use the supplied Classic and DiffSinger assets
without redistributing or modifying their original banks. See
[`voice-smoke.md`](docs/microtonal/voice-smoke.md) for the method and results.

### Verification status

At the recorded checkpoint, 363 automated cases pass. The ordinary run explicitly
skips the external-voice theory; its separate Classic, Worldline-R and DiffSinger
run passes all three cases with an empty render cache. Their nine generated tones
are within 0.75 cent of the requested stable pitches under the documented method.

Actual ENUNU, Vogen and VOICEVOX synthesis, real MIDI hardware, broader UI and
high-DPI coverage, and DiffSinger automatic-pitch/retake combinations remain open.
See the [production record](docs/microtonal/PRODUCTION.md) for details. The first
release is not declared complete while these checks remain open.

### Contributing

- Work happens on `develop`. Create `feature/<topic>` or `fix/<topic>` from it and
  open a pull request into `develop` (not the default `main`). The pull-request
  tests must pass.
- `main` is the default branch and always points to the latest released version
  (`vX.Y.Z` tags); use it to try a stable state.
- Build and test serially with the commands above. Follow `AGENTS.md` for writing,
  versioning and code conventions, and `DESIGN.md` for colours.
- Japanese documents are checked with the yomiyasu skill before merging. If you do
  not have it, mention the Japanese changes in your pull request.

### Attribution

Upstream code remains under its original MIT licence. The workspace layout
(welcome screen, docked piano roll, top bar and side toolbar) and pitch follow are
reimplemented from [OpenUtau Lunai](https://github.com/keirokeer/OpenUtau-lunai)
(MIT, commit `01be298`). Accidentals use Xen Accidentals, a Modified Version of
Bravura Text under SIL OFL 1.1, renamed because "Bravura" is a Reserved Font Name.
It keeps only the accidental glyphs, rescaled to sit on the baseline, and its
licence is in `OpenUtau/Assets/Fonts/`. `tools/build-xen-accidentals.py` builds it
from the unmodified Bravura Text in `tools/fonts/`, retrieved from Steinberg's
Bravura repository. Voicebanks, external
resamplers and their licences remain separate from this repository and package.
Xen's app data, installer identity and `.ustxen` association are distinct from
upstream OpenUtau; automatic upstream updates are disabled for this derivative.

### About upstream OpenUtau

OpenUtau is a free, open-source editor made for the UTAU community. The links
below belong to the upstream project; its downloads do not include Xen features.

[![Build](https://img.shields.io/github/actions/workflow/status/stakira/OpenUtau/build.yml?style=for-the-badge)](https://github.com/stakira/OpenUtau/actions/workflows/build.yml)
[![Discord](https://img.shields.io/discord/551606189386104834?style=for-the-badge&label=discord&logo=discord&logoColor=ffffff&color=7389D8&labelColor=6A7EC2)](https://discord.gg/UfpMnqMmEM)
[![QQ Qroup](https://img.shields.io/badge/QQ-485658015-blue?style=for-the-badge)](https://qm.qq.com/cgi-bin/qm/qr?k=8EtEpehB1a-nfTNAnngTVqX3o9xoIxmT&jump_from=webapi)
[![Trello](https://img.shields.io/badge/trello-go-blue?style=for-the-badge&logo=trello)](https://trello.com/b/93ANoCIV/openutau)

#### Getting started

[![Download](https://img.shields.io/static/v1?style=for-the-badge&logo=github&label=download&message=windows-x64&labelColor=FF347C&color=4ea6ea)](https://github.com/stakira/OpenUtau/releases/latest/download/OpenUtau-win-x64.zip)</br>
[![Download](https://img.shields.io/static/v1?style=for-the-badge&logo=github&label=download&message=linux-x64&labelColor=FF347C&color=4ea6ea)](https://github.com/stakira/OpenUtau/releases/latest/download/OpenUtau-linux-x64.zip)

It is **strongly recommended** that you read these GitHub wiki pages before using the software.
- [Getting-Started](https://github.com/stakira/OpenUtau/wiki/Getting-Started)
- [Resamplers](https://github.com/stakira/OpenUtau/wiki/Resamplers-and-Wavtools)
- [Phonemizers](https://github.com/stakira/OpenUtau/wiki/Phonemizers)
- [FAQ](https://github.com/stakira/OpenUtau/wiki/FAQ)

- [中文使用说明](https://opensynth.miraheze.org/wiki/OpenUTAU/%E4%BD%BF%E7%94%A8%E6%96%B9%E6%B3%95)

#### How to contribute

Tried OpenUtau and not satisfied? Don't just walk away! You can help:
- Report issues on the [Discord server](https://discord.gg/UfpMnqMmEM) or GitHub.
- Suggest features on Discord or GitHub.
- Add or update translations for your language on [Crowdin](https://crowdin.com/project/oxygen-dioxideopenutau).

Know how to code? Got an idea for an improvement? Don't keep it to yourself!
- Contribute fixes via pull requests.
- Check out the development roadmap on [Trello](https://trello.com/b/93ANoCIV/openutau) and discuss it on Discord.

#### Plugin development

Want to contribute plugins to help other users? Check out the API documentation:
- [Editing Macros API Document](OpenUtau.Core/Editing/README.md)
- [Phonemizers API Document](OpenUtau.Core/Api/README.md)

#### Main features

Navigate the interface naturally and fluently using the mouse and scroll wheel. Keyboard shortcuts are also available.

![Editor](Misc/GIFs/editor.gif)

Easily create songs and covers using the feature-rich MIDI editor.

![Editor](Misc/GIFs/editor2.gif)

Create expressive vibratos with the easy-to-use vibrato editor.

![Vibrato](Misc/GIFs/vibrato.gif)

Pre-rendering and built-in resamplers let you quickly preview your work.

![Playback](Misc/GIFs/playback.gif)

See the [Getting-Started wiki page](https://github.com/stakira/OpenUtau/wiki/Getting-Started) for more.

#### All features
- Modern user experience.
- Easy navigation using the mouse and keyboard.
- Feature-rich MIDI editor.
  - Support for importing VSQX (Vocaloid 4) tracks.
- Selective backward compatibility with UTAU.
  - OpenUtau aims to solve problems with fewer steps. It is not designed to replicate UTAU features exactly.
- Extensible real-time phonetic editing.
  - Includes phonemisers for different phonetic systems (VCV, CVVC, Arpasing, etc.) in many different languages (English, Japanese, Chinese, Korean, Russian and more).
- Expressions replace the standard UTAU "flags" for tuning.
  - The built-in WORLDLINE-R resampler supports curve tuning, similar to many vocal synth editors.
- Internationalisation, including UI translation and file system encoding support.
  - Unlike UTAU, there is no need to change your system locale to use OpenUtau.
- Smooth preview/rendering experience.
  - Pre-rendering allows OpenUtau to render vocals before playback, saving time during editing and tuning.
- Supports ENUNU AI singers. See the [ENUNU wiki page](https://github.com/stakira/OpenUtau/wiki/ENUNU-NNSVS-Support) for more info.
- Easy-to-use plugin system.
- Versatile resampling engine interface.
  - Compatible with most UTAU resamplers.
- Runs on Windows (32/64 bit), macOS, and Linux.

##### What it doesn't do
- While OpenUtau can do very minimal mixing, it will not replace your digital audio workstation of choice.
- OpenUtau does not aim for Vocaloid compatibility, except for some limited features.

<a id="japanese"></a>

## 日本語

**[English](#english)** | **[日本語](#japanese)**

OpenUtau Xen は [OpenUtau](https://github.com/stakira/OpenUtau) を基にした
ローカル派生版で、微分音と多様な音律による歌声合成を扱います。基点は本家のコミット
`3eb9dd26459fa51dc2c5bf46693a9492d817c130` です。現在は開発版で、初版の受入計画は
[`docs/microtonal/PLAN.md`](docs/microtonal/PLAN.md) で管理しています。

Windows・macOS・Linux 向けの最新版は
[Releases](https://github.com/palf-gh/OpenUtau-Xen/releases/latest) からダウンロードできます。
バージョンを上げるたびに Releases で公開し、各バージョンの内容は
[`PRODUCTION.md`](docs/microtonal/PRODUCTION.md) に記録しています。

### 編集とファイル

- プロジェクト全体の既定音律は **Project → Project tuning** で、トラックごとの上書きは
  ピアノロールの音律メニューで設定します。A4 の周波数を含む基準音も変更できます。
- **Apply tuning** は既存ノートを新しい音律の最寄りの degree へ合わせます。
  **Change reference** は degree を保ったまま基準を変えます。**Apply display** は
  使用するスケールだけを変えます。既存の周波数を保つ選択肢では、ノートを
  degree と残差セントで表します。
- `.ustxen` は音律定義を埋め込んだプロジェクト全体を保存します。`.tunxen` は
  再利用できる音律・スケール・記法・鍵盤設定のプリセットを保存します。
- 本家の `.ustx`、Scala の `.scl`・`.kbm` を読み込めます。互換 USTX は別ファイルとして
  書き出します。MIDI 書出しは半音単位に量子化され、UST は変換後の音高差をピッチ曲線に
  含めます。交換形式では Xen 固有の情報をすべては保持できません。
- 行の表示方式（すべての音・スケールを強調・スケールを圧縮表示）と、鍵盤の主表記・副表記は、
  ピアノロールの **View** メニューで選びます。これらはエディターの設定で、音律と
  プロジェクトは変わりません。どの表示方式でも連続した実際の音高を保ち、圧縮表示では
  スケール外にある既存ノート用の行を加えます。
- 鍵盤の表記は、検証済みの対応表がある音律（12・24・31・53 平均律と 12 音のプリセット）では
  Stein–Zimmermann–Gould、トリターブの等分平均律では Bohlen–Pierce の Lambda 音名
  （C D E F G H J A B）、ほかの平均律では Ups and Downs を既定にしています。
- 鍵盤は 3 色で表示します。幹音は白、シャープとフラットは黒、その間の微分音は青で、
  31 音の Fokker オルガンにならっています。配置は音律ごとの生成音程から計算し、
  分割数に応じて広がります（[鍵盤の仕様書](docs/microtonal_keyboard_algorithm_spec.md)）。
- ノートのプロパティの **Tone** 欄には幹音に音律の段数を足し引きした形
  （53 平均律なら `C4`、`C4+1` … `C4+4`、`D4-4` … `D4-1`、`D4`）を入力できます。
  `C#4`・`Db4` のような音名や、`+3` のような相対的な段数も使えます。
- 音律と MIDI の操作部、警告、プリセット名は、登録済みの 20 言語すべてに翻訳しています。
  元言語の識別子 en-US の文言はイギリス英語です。

### ビルドとテスト

.NET 10 と、本家と同じネイティブ実行時依存が必要です。

```powershell
dotnet build OpenUtau
dotnet test OpenUtau.Test
dotnet publish OpenUtau -c Release -r win-x64 --self-contained true
```

外部音源を使うテストは任意実行で、記録済みのローカル音源が必要です。通常のテストの後に、
単独で実行してください。

```powershell
pwsh -NoProfile -File tools/verify-xen-voices.ps1 -ClassicBank '<UTAU voicebank>' -DiffSingerBank '<DiffSinger configs>' -DiffSingerVocoder '<vocoder.onnx>' -UtauEngine '<folder with resampler.exe>'
```

この合成スモークテストは提供された Classic と DiffSinger の音源を使います。元の音源は
変更も再配布もしません。手法と結果は [`voice-smoke.md`](docs/microtonal/voice-smoke.md)
にあります。

### 検証状況

記録時点で、自動テスト 363 件が成功しています。通常の実行では外部音源のテストを明示的に
スキップします。別途 Classic・Worldline-R・DiffSinger を実行し、レンダーキャッシュが
空の状態で 3 件とも成功しました。生成した 9 音は記録した手法で測ると、いずれも
指定した安定音高との差が 0.75 セント以内でした。

ENUNU・Vogen・VOICEVOX での実際の合成、実機 MIDI 鍵盤、より広い UI と高 DPI の確認、
DiffSinger の自動ピッチとリテイクの組合せは未検証です。詳細は
[制作記録](docs/microtonal/PRODUCTION.md) を参照してください。これらが残っている間は、
初版の完成とはしません。

### 開発に参加するには

- 開発は `develop` で行います。ここから `feature/<内容>` か `fix/<内容>` のブランチを作り、
  `develop` へのプルリクエストを送ってください（既定の `main` ではありません）。
  プルリクエストのテストが通る必要があります。
- `main` は既定ブランチで、常に最新の公開バージョン（`vX.Y.Z` タグ）を指します。
  安定した状態を試すときは `main` を使ってください。
- ビルドとテストは、上のコマンドで 1 つずつ順に実行してください。文章、バージョン、コードの
  決まりは `AGENTS.md` に、配色は `DESIGN.md` にあります。
- 日本語の文書はマージの前に yomiyasu スキルで確認します。yomiyasu を使えない場合は、
  日本語を変更したことをプルリクエストに書いてください。

### 帰属表示

本家のコードは元の MIT ライセンスのままです。ワークスペースの画面（ウェルカム画面、
組み込みのピアノロール、上部バー、縦ツールバー）とピッチ追従は、
[OpenUtau Lunai](https://github.com/keirokeer/OpenUtau-lunai)（MIT、コミット `01be298`）を
参考に作り直したものです。変化記号には Xen Accidentals を使います。これは Bravura Text を
SIL OFL 1.1 に基づいて改変したフォントで、「Bravura」が予約フォント名のため名前を変えています。
変化記号の字形だけを残し、大きさと位置を直してベースラインに乗るようにしました。ライセンスは
`OpenUtau/Assets/Fonts/` に置いています。`tools/build-xen-accidentals.py` が、`tools/fonts/` にある
無改変の Bravura Text からこのフォントを作ります。Bravura Text は Steinberg の Bravura
リポジトリから取得しました。音源、外部リサンプラーとそれらの
ライセンスは、このリポジトリと配布物には含めません。Xen のアプリデータ、インストーラーの
識別情報、`.ustxen` の関連付けは本家 OpenUtau と分けています。この派生版では本家への
自動更新を無効にしています。

### 本家 OpenUtau

OpenUtau は UTAU コミュニティ向けに作られた無料のオープンソースエディターです。
以下のリンクは本家のもので、そのダウンロードには Xen の機能は含まれません。

#### はじめに

[Windows x64 版](https://github.com/stakira/OpenUtau/releases/latest/download/OpenUtau-win-x64.zip)
・[Linux x64 版](https://github.com/stakira/OpenUtau/releases/latest/download/OpenUtau-linux-x64.zip)
をダウンロードできます。使い始める前に、次の GitHub Wiki を読むことを**強くお勧めします**。
- [Getting-Started](https://github.com/stakira/OpenUtau/wiki/Getting-Started)
- [Resamplers](https://github.com/stakira/OpenUtau/wiki/Resamplers-and-Wavtools)
- [Phonemizers](https://github.com/stakira/OpenUtau/wiki/Phonemizers)
- [FAQ](https://github.com/stakira/OpenUtau/wiki/FAQ)

#### 貢献するには

OpenUtau を試して不満があれば、次の方法で改善に協力できます。
- [Discord サーバー](https://discord.gg/UfpMnqMmEM)や GitHub で不具合を報告する。
- Discord や GitHub で機能を提案する。
- [Crowdin](https://crowdin.com/project/oxygen-dioxideopenutau) で自分の言語の翻訳を追加・更新する。

コードを書ける場合は、次の方法もあります。
- プルリクエストで修正を送る。
- [Trello](https://trello.com/b/93ANoCIV/openutau) の開発ロードマップを確認し、Discord で議論する。

#### プラグイン開発

ほかの利用者に役立つプラグインを作る場合は、次の API 文書を参照してください。
- [編集マクロ API 文書](OpenUtau.Core/Editing/README.md)
- [フォニマイザ API 文書](OpenUtau.Core/Api/README.md)

#### 主な機能

- マウスとスクロールホイールで自然に操作でき、キーボードショートカットも使えます。
- 機能の豊富な MIDI エディターで、楽曲やカバーを作れます。
- ビブラートエディターで、表情のあるビブラートを付けられます。
- 先行レンダリングと内蔵リサンプラーで、作業中の音をすぐ確認できます。

各機能の動画は[英語版](#english)にあります。詳しくは
[Getting-Started](https://github.com/stakira/OpenUtau/wiki/Getting-Started) を参照してください。

#### 全機能

- 現代的な操作性。
- マウスとキーボードによる簡単な操作。
- 機能の豊富な MIDI エディター。
  - VSQX（Vocaloid 4）トラックの読込み。
- UTAU との選択的な後方互換性。
  - OpenUtau は少ない手順で問題を解決することを目指しています。UTAU の機能をそのまま再現するための設計ではありません。
- 拡張可能なリアルタイム音素編集。
  - 英語・日本語・中国語・韓国語・ロシア語などの、さまざまな音素体系（VCV、CVVC、Arpasing など）に対応したフォニマイザを同梱しています。
- 調声には UTAU の標準「フラグ」の代わりにエクスプレッションを使います。
  - 内蔵リサンプラー WORLDLINE-R は、多くの歌声合成エディターと同様の曲線による調声に対応しています。
- UI の翻訳とファイルシステムの文字コード対応を含む国際化。
  - UTAU と違い、システムロケールを変更する必要はありません。
- 快適な試聴・レンダリング。
  - 再生前に先行レンダリングするため、編集や調声の待ち時間を減らせます。
- ENUNU の AI シンガーに対応しています。詳しくは [ENUNU の Wiki](https://github.com/stakira/OpenUtau/wiki/ENUNU-NNSVS-Support) を参照してください。
- 使いやすいプラグインの仕組み。
- 用途の広いリサンプリングエンジンのインターフェイス。
  - ほとんどの UTAU リサンプラーと互換性があります。
- Windows（32/64 ビット）、macOS、Linux で動作します。

##### できないこと

- 簡単なミキシングはできますが、DAW の代わりにはなりません。
- 一部の機能を除き、Vocaloid との互換性は目指していません。
