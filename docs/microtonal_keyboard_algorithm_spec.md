# 微分音ピアノロール鍵盤生成アルゴリズム仕様書

**Version 0.1**  
対象: OpenUtau等のピアノロールにおける EDO / EDT 鍵盤表示

---

## 1. 目的

本仕様は、任意の等分平均律に対して、ピアノロール左端に表示する鍵を以下の3種類へ機械的に分類するアルゴリズムを定義する。

- **White**
  - 基準音階の nominal / natural に対応する領域
- **Black**
  - conventional accidental、すなわち sharp / flat に対応する領域
- **Between**
  - White / Black の中間に存在する微分音領域

重要なのは、White / Black を特定の pitch step に固定された「点」として扱わないことである。

分割数が高くなるにつれて、White / Black はそれぞれの理論上のアンカーを中心として**有限の幅を持つ領域**へ拡張する。

ただし、

- natural 間を無条件に White で埋めない
- sharp と flat の間を無条件に Black で埋めない

ものとする。

この原則により、例えば

- 31-EDOでは Fokker 型の `7 White + 10 Black + 14 Between`
- 41-EDOでは E-F / B-C 内部が Between
- 72-EDOでは通常音の周囲へ White / Black が適度に拡張
- 73-EDOでは D♭ と C♯ の周囲は Black だが、その中央は Between

という形を生成する。

31音Fokker organは実際に7個の白鍵、♯と♭を分離した10個の黒鍵、残る14個の青鍵を用いる。したがって31-EDOを「理想的な基準解像度」の一つとして採用する。

参考: https://huygens-fokker.org/en/fokkerorgan/

---

## 2. 基本モデル

すべての鍵盤生成を、

\[
\text{Pitch system}
\rightarrow
\text{Theoretical anchors}
\rightarrow
\text{Morphological regions}
\]

の3段階へ分離する。

音律理論とUI形状を直接結合してはならない。

内部データとして最低限、各stepについて以下を保持する。

```text
PitchKey {
    step: int
    role: WHITE | BLACK | BETWEEN
    anchorRole: NONE | NATURAL | SHARP | FLAT
    label: string?
    isAnchor: bool
    sourceAnchorStep: int?
    distanceFromAnchor: int?
}
```

`role` と `anchorRole` は別概念とする。

例えばC♯の隣接stepがBlackへ拡張された場合、

```text
role       = BLACK
anchorRole = NONE
isAnchor   = false
```

である。

---

## 3. 共通 equave モデル

等分平均律は、

\[
n
\]

個の等しいstepによってequave

\[
R:1
\]

を分割したものとして表現する。

step \(k\) の周波数は

\[
f(k)=f_0R^{k/n}
\]

とする。

本仕様では少なくとも、

```text
R = 2   octave / EDO
R = 3   tritave / EDT
```

をサポートする。

pitch-classの計算は

\[
k\bmod n
\]

で行い、鍵盤分類もequave境界を循環するものとする。

したがって基準音C等のWhite領域は、

```text
... n-1 | 0 | 1 ...
```

の境界を跨いで拡張可能である。

---

## 4. Octave family: n-EDO

### 4.1 生成五度

\(n\)-EDOについて、3:2に最も近い五度を

\[
g=
\operatorname{round}
\left(
n\log_2\frac32
\right)
\]

stepとする。

手動指定を許可してもよい。

---

### 4.2 7音ダイアトニック骨格

五度 \(g\) から、

#### large diatonic step

\[
L=2g-n
\]

#### small diatonic step

\[
s=3n-5g
\]

#### chromatic alteration

\[
c=L-s
\]

を求める。

同値な式として

\[
c=7g-4n
\]

を使用してよい。

このとき

\[
n=5L+2s
\]

である。

基準音階は、

```text
C  D  E  F  G  A  B  C
 L  L  s  L  L  L  s
```

とする。

すなわちnatural anchorは、

\[
\begin{aligned}
C&=0\\
D&=L\\
E&=2L\\
F&=2L+s\\
G&=3L+s\\
A&=4L+s\\
B&=5L+s
\end{aligned}
\]

となる。

---

### 4.3 EDOレイアウトの有効条件

ピアノ型レイアウトを成立させるには最低限、

\[
L>0,\qquad s>0,\qquad c>0
\]

を要求する。

満たさない場合、この鍵盤生成方式を使用してはならない。

代わりに、

```text
MOS layout
Uniform layout
Custom layout
```

等へフォールバックする。

---

## 5. EDO accidental anchors

large interval、

```text
C-D
D-E
F-G
G-A
A-B
```

の5区間に対してaccidental anchorを生成する。

lower naturalを \(x\) とする。

sharp anchor:

\[
x_\sharp=x+c
\]

flat anchor:

\[
x_\flat=x+L-c=x+s
\]

したがってlarge interval内では、

\[
0,\quad s,\quad c,\quad L
\]

が基本構造となる。

\(s<c\) の場合、

```text
natural → flat → sharp → natural
```

となる。

\(c<s\) の場合、

```text
natural → sharp → flat → natural
```

となる。

\(c=s\) の場合、

```text
sharp = flat
```

となる。

これによってenharmonic orderingをEDOごとに自動的に反転できる。

### 例

31-EDO:

\[
g=18,\quad L=5,\quad s=3,\quad c=2
\]

```text
C   ·   C♯  D♭  ·   D
0   1    2   3   4   5
```

53-EDO:

\[
g=31,\quad L=9,\quad s=4,\quad c=5
\]

```text
C · · · D♭ C♯ · · · D
0       4  5       9
```

73-EDO:

\[
g=43,\quad L=13,\quad s=4,\quad c=9
\]

```text
C ... D♭ .... C♯ ... D
0     4       9     13
```

sharp / flatの順序を31-EDOから流用してはならない。

---

## 6. White / Black anchor

### White anchor

natural pitchのみをWhite anchorとする。

```text
C D E F G A B
```

### Black anchor

生成されたsharp / flat pitchをBlack anchorとする。

```text
C♯ D♭
D♯ E♭
F♯ G♭
G♯ A♭
A♯ B♭
```

同一stepへsharpとflatが一致する場合は、一つのBlack anchorとして統合する。

### Between

その他のpitchは、初期状態ではすべてBetweenとする。

---

## 7. 領域拡張

### 7.1 基本思想

White / Blackはanchorそのものだけではなく、その周辺のpitchを同じ鍵盤階層へ取り込んでよい。

ただし領域サイズを、

```text
現在のnに対する恣意的な比率
```

で決定してはならない。

31-EDOのFokker型配置をoctave familyにおける**基準解像度**とする。

\[
N_{\mathrm{ref}}=31
\]

31-EDOでは1stepそのものが一つの基準鍵領域である。

したがって基準鍵の正規化幅は

\[
\frac1{31}
\]

equaveである。

その半幅をtarget EDOへ変換して、

\[
\rho=
\frac{n}{2N_{\mathrm{ref}}}
=
\frac{n}{62}
\]

とする。

整数grid上での拡張半径は、

\[
r=\left\lfloor\rho\right\rfloor
\]

とする。

---

### 7.2 代表値

```text
31-EDO → r = 0
41-EDO → r = 0
53-EDO → r = 0
62-EDO → r = 1
72-EDO → r = 1
73-EDO → r = 1
93-EDO → r = 1
124-EDO → r = 2
```

したがって31-EDOそのものでは、

- natural = 1 White step
- sharp / flat = 1 Black step
- 残り = Between

となり、Fokker型を保つ。

一方72 / 73-EDOではWhite / Black anchorの前後1stepまで同色領域へ拡張される。

---

## 8. Anchor dilation

各White / Black anchorについて、

\[
-r,\ldots,+r
\]

stepの候補領域を作る。

ただし以下を厳守する。

### 8.1 Anchor保護

拡張領域は別のexact anchorを上書きしてはならない。

優先順位は、

```text
exact anchor > expanded region
```

とする。

### 8.2 異色競合

あるpitchがWhite anchorとBlack anchorの双方の拡張範囲へ入った場合、各anchorまでの循環step距離を比較する。

近いanchorの色を採用する。

距離が完全に等しい場合は、

```text
BETWEEN
```

とする。

これによりWhite / Black境界が恣意的に片方へ偏らない。

### 8.3 White同士はbridgeしない

二つのnatural anchorの間を、単に両方ともWhiteであるという理由だけで埋めてはならない。

例えば41-EDOでは、

\[
s=3
\]

なのでE-Fは、

```text
E  Between  Between  F
```

となる。

E-F区間全体をWhiteにしてはならない。

---

## 9. Black accidental cluster の short-gap closure

Blackについてのみ、一つ追加規則を設ける。

同一large interval内の

```text
lower sharp
upper flat
```

は一つのaccidental clusterを形成するものとみなす。

anchor dilation後、両Black領域の間に残ったBetween step数を

\[
b
\]

とする。

### 条件

\[
b\le1
\]

の場合のみ、その空隙をBlackで埋めてよい。

\[
b\ge2
\]

の場合は埋めない。

これを **short-gap closure** と呼ぶ。

---

### 9.1 22-EDO

22-EDO:

\[
g=13,\quad L=4,\quad s=1,\quad c=3
\]

```text
C
D♭
?
C♯
D
```

Black anchors間には1stepしかない。

したがってshort-gap closureにより、

```text
C
[ D♭  black  C♯ ]
D
```

となる。

これは22-EDOについて提案されている「通常の各黒鍵を3分割する」鍵盤構造とも整合する。

参考: https://xenreference.com/w/22edo

---

### 9.2 73-EDO

73-EDO:

\[
L=13,\quad s=4,\quad c=9
\]

かつ

\[
r=1
\]

である。

C-D間では、

```text
0  C
1  White expansion
2  Between
3  Black expansion
4  D♭
5  Black expansion
6  Between
7  Between
8  Black expansion
9  C♯
10 Black expansion
11 Between
12 White expansion
13 D
```

となる。

D♭とC♯のBlack領域の間にはBetweenが2step残るため、

```text
short-gap closure = false
```

である。

したがって、

**D♭からC♯までをすべてBlackにしてはならない。**

---

## 10. 重要なEDOテストケース

### 12-EDO

```text
g = 7
L = 2
s = 1
c = 1
r = 0
```

sharp = flat。

結果は通常ピアノと一致する。

```text
White   7
Black   5
Between 0
```

### 24-EDO

```text
g = 14
L = 4
s = 2
c = 2
r = 0
```

sharp = flat。

natural / conventional accidental以外のquarter-tone pitchがBetweenとなる。

### 31-EDO

```text
g = 18
L = 5
s = 3
c = 2
r = 0
```

結果:

```text
White   7
Black   10
Between 14
```

Fokker organの主要な色分類と一致する。

参考: https://huygens-fokker.org/en/fokkerorgan/

### 41-EDO

```text
g = 24
L = 7
s = 3
c = 4
r = 0
```

C-D:

```text
C
Between
Between
D♭
C♯
Between
Between
D
```

E-F:

```text
E
Between
Between
F
```

41-EDOには既存の12-column型鍵盤など複数の構成が存在するため、本仕様はそれらを物理的に再現することではなく、1次元ピアノロールへ理論構造を投影することを目的とする。

参考: https://en.xen.wiki/w/41edo_Keyboards

### 72-EDO

```text
g = 42
L = 12
s = 6
c = 6
r = 1
```

sharp = flat。

各White / Black anchorが前後1stepへ拡張する。

従来案のように、

```text
White   = 7 pitchだけ
Black   = 5 pitchだけ
Between = 残り60
```

にはならない。

---

# 11. Tritave family: n-EDT / Bohlen-Pierce

## 11.1 基本方針

3:1 tritaveをequaveとする等分平均律については、西洋7音ダイアトニックをそのまま転用してはならない。

Bohlen-Pierce体系を基準とする。

Bohlen-Pierce equal temperamentは3:1 tritaveを13等分した13-EDTであり、現在Lambda modeがBPの基準diatonic modeとして扱われる。Lambdaは9音である。

参考: https://huygens-fokker.org/en/tuningsystems/

BPでは9/7が西洋体系における五度に近い生成的役割を持ち、key circleも9/7を基礎として構成される。

参考: https://www.huygens-fokker.org/bpsite/notation.html

したがってn-EDTでは、octave familyの3:2 generatorに代えて、

\[
\frac97
\]

をgenerator targetとする。

---

## 12. EDT generator

\(n\)-EDTについて、

\[
g=
\operatorname{round}
\left(
n\log_3\frac97
\right)
\]

とする。

すなわち、

\[
g=
\operatorname{round}
\left(
n\frac{\ln(9/7)}{\ln3}
\right)
\]

である。

13-EDTでは、

\[
g=3
\]

となる。

BPSのcanonical generatorも13-EDTの3stepに対応する。

参考: https://en.xen.wiki/w/BPS

---

## 13. Bohlen-Pierce Lambda MOS

Lambda familyは9音で、

\[
4L+5s
\]

型のMOSとして扱える。

generatorについて、

\[
g=L+s
\]

であるため、

\[
L=5g-n
\]

\[
s=n-4g
\]

を得る。

したがって、

\[
n=4L+5s
\]

である。

13-EDT:

\[
g=3,\quad L=2,\quad s=1
\]

となる。

Huygens-Fokker Foundationが示すLambda modeの13-EDTパターン

```text
2112 1 2121
```

は、本仕様では

```text
L s s L s L s L s
```

に対応する。

参考: https://www.huygens-fokker.org/bpsite/modes.html

---

## 14. BP natural anchors

C-Lambdaを基準とする。

natural nominalを、

```text
C D E F G H J A B C
```

の9音とする。

Iは使用しない。

ステップ構造:

```text
C-D : L
D-E : s
E-F : s
F-G : L
G-H : s
H-J : L
J-A : s
A-B : L
B-C : s
```

すなわちlarge intervalは4箇所、

```text
C-D
F-G
H-J
A-B
```

である。

13-EDTの標準BP chromatic pitchにも、これらlarge interval中に

```text
C♯/D♭
F♯/G♭
H♯/J♭
A♯/B♭
```

が存在する。13-EDTの全13 pitchは9 natural + 4 accidentalとして表現可能である。

参考: https://xenreference.com/w/13edt

---

## 15. BP accidental displacement

BPのlarge stepについて、

\[
c=L-s
\]

をaccidental displacementと定義する。

lower nominal \(x\) から、

\[
x_\sharp=x+c
\]

upper nominalからflat方向へ \(c\) 下げるので、

\[
x_\flat=x+L-c=x+s
\]

となる。

したがって各large intervalは、

\[
0,\quad s,\quad c,\quad L
\]

という、octave familyと同型の局所構造を持つ。

### 13-EDT

\[
L=2,\quad s=1,\quad c=1
\]

なので、

```text
C
C♯/D♭
D
```

となる。

13-EDTでは9 natural + 4 accidentalで13 pitchすべてが埋まる。

---

## 16. BP familyの有効条件

最低限、

\[
L>0,\qquad s>0,\qquad c>0
\]

を満たすこと。

満たさない場合、そのn-EDTはBohlen-Pierce Lambda型鍵盤として表示してはならない。

代替として、

```text
Uniform EDT
Alternative MOS
Custom scale
```

を使用する。

---

## 17. n-EDTにおける White / Black / Between

octave familyと同じ表示原理を用いる。

### White

9個のLambda natural anchors。

### Black

4つのlarge intervalに生成されたsharp / flat anchors。

### Between

その他。

ただし13-EDTではsharpとflatが一致するため、

```text
White   9
Black   4
Between 0
```

となる。

分割数が増えるとnatural / accidental anchorsの間へ新しいpitchが出現し、Betweenが生成される。

---

## 18. BP領域拡張

BP familyの基準解像度はcanonicalな

\[
N_{\mathrm{ref,BP}}=13
\]

とする。

White / Black anchorの正規化基準幅は、

\[
\frac1{13}
\]

tritave。

したがって拡張半径を、

\[
\rho_{\mathrm{BP}}
=
\frac{n}{26}
\]

\[
r_{\mathrm{BP}}
=
\left\lfloor
\frac{n}{26}
\right\rfloor
\]

とする。

13-EDTでは、

\[
r=0
\]

であり、canonical BP keyboardを変更しない。

26以上のEDTでは、White / Black領域が徐々に前後へ広がる。

---

## 19. BP accidental short-gap closure

EDO familyと全く同じ規則を使用する。

各large interval内のsharp / flatを一組のBlack clusterとする。

anchor dilation後にBlack領域間へ残るBetweenが、

\[
0\text{ または }1
\]

stepの場合のみBlackへ統合する。

2step以上ならBetweenを残す。

White anchor同士はbridgeしない。

---

## 20. 共通 Regionizer

最終実装ではEDO / EDTそれぞれが直接色を決定してはならない。

各familyは、

```text
AnchorSet
ReferenceDensity
PairedBlackAnchors
```

だけをRegionizerへ渡す。

概念的には、

```text
generatePitchSystem()
    ↓
generateNaturalAnchors()
    ↓
generateAccidentalAnchors()
    ↓
dilateAnchors()
    ↓
resolveConflicts()
    ↓
closeShortBlackGaps()
    ↓
remaining = BETWEEN
```

とする。

---

## 21. Family定義

### Octave / Western-diatonic family

```text
equave             = 2/1
referenceDivision  = 31
generatorTarget    = 3/2
naturalCount       = 7
largeGapCount      = 5
smallGapCount      = 2
naturalLabels      = C D E F G A B
```

### Tritave / Bohlen-Pierce family

```text
equave             = 3/1
referenceDivision  = 13
generatorTarget    = 9/7
naturalCount       = 9
largeGapCount      = 4
smallGapCount      = 5
naturalLabels      = C D E F G H J A B
referenceMode      = Lambda
```

---

## 22. 色分類と音名を分離する

表示色とnotationは別サブシステムとする。

例えば73-EDOにおける、

```text
step 5
```

がBlack領域であっても、それ自体がexact D♭やC♯でなければ、勝手にD♭等の名前を付けてはならない。

分類:

```text
role = BLACK
```

音名:

```text
label = null
```

は合法である。

必要ならUI表示として、

```text
D♭+1
C♯-4
```

等の相対表記を別途生成する。

---

## 23. Anchor衝突

同一stepが異なる種類のexact anchorとして生成された場合、

```text
White + Black
```

を一方へ黙って優先してはならない。

内部的に、

```text
anchorConflict = true
```

を立てる。

UI分類は原則Betweenとし、tooltip等で複数のspellingを表示する。

ただし同色、

```text
Sharp + Flat
```

の衝突は正常なenharmonic coincidenceなのでBlackとして統合する。

---

## 24. アルゴリズム擬似コード

```text
function generateKeyboard(n, family):

    theory = family.deriveTheory(n)

    if !theory.valid:
        return fallbackLayout(n)

    anchors = []

    for natural in theory.naturals:
        anchors += Anchor(
            step = natural.step,
            colour = WHITE,
            label = natural.name
        )

    for largeGap in theory.largeGaps:
        sharp = largeGap.start + theory.chromaticShift
        flat  = largeGap.start + theory.smallStep

        anchors += Anchor(sharp, BLACK, sharpName)
        anchors += Anchor(flat,  BLACK, flatName)

    mergeSameStepAnchors()

    role[0 .. n-1] = BETWEEN

    for exact anchor:
        role[anchor.step] = anchor.colour

    radius =
        floor(
            n /
            (2 * family.referenceDivision)
        )

    for every non-anchor step:
        whiteDistance =
            nearest WHITE anchor distance
            within radius

        blackDistance =
            nearest BLACK anchor distance
            within radius

        if only white exists:
            role = WHITE

        else if only black exists:
            role = BLACK

        else if both exist:
            if whiteDistance < blackDistance:
                role = WHITE

            else if blackDistance < whiteDistance:
                role = BLACK

            else:
                role = BETWEEN

    for each paired sharp/flat cluster:

        gap =
            number of BETWEEN steps
            between their expanded BLACK components

        if gap <= 1:
            fill gap with BLACK

    return role[]
```

---

## 25. 必須テスト

実装時には少なくとも以下を自動テストする。

```text
12-EDO
→ conventional piano

22-EDO
→ black accidental cluster が3連続になる

31-EDO
→ 7 White / 10 Black / 14 Between

41-EDO
→ E-F内部をWhiteで埋めない

53-EDO
→ D♭ < C♯

72-EDO
→ White / Blackがanchor前後へ1step拡張

73-EDO
→ D♭およびC♯の前後1stepはBlack
→ D♭-C♯中央にはBetweenが残る

13-EDT
→ BP Lambda
→ 9 White / 4 Black / 0 Between

31-EDT
→ Lambda 4L+5s構造から生成
→ 13-EDTの単純スケーリングは禁止

53-EDT
→ g, L, s, cを53から再計算
→ 13-EDT pitch位置の標本拡大は禁止
```

---

## 26. 禁止事項

以下の方式は採用しない。

```text
7 White + 残り全部Black

12-EDO鍵盤を単純にnearest-neighbour射影

31-EDOの31セルを任意nへ再標本化

natural間を全てWhiteで充填

sharp-flat間を常に全てBlackで充填

nの増加に比例してBlackだけを一方的に増やす

EDTへ12-EDO / 31-EDOの音名構造をそのまま適用
```

---

## 27. 設計上の位置付け

本仕様のWhite / Black / Betweenは、

**物理鍵盤を完全再現する分類ではない。**

目的は、

> 任意の高分割等分平均律を、既存のピアノロールUIにおいて音楽理論上の階層を失わず視認可能にする

ことである。

Octave familyでは31-tone Fokker keyboardを重要な参照例とするが、その31個の色列自体をテンプレートとして拡大しない。

31-tone systemから採用するのは、

```text
natural
conventional accidental
intervening microtone
```

という三層構造である。

参考: https://huygens-fokker.org/en/fokkerorgan/

Tritave familyについても同様に、13-EDTの13セルそのものを拡大するのではなく、

```text
Lambda natural
Lambda accidental
intervening pitch
```

という構造を一般化する。

Bohlen-Pierceでは3:1 tritave、13-EDT、9音Lambda mode、9/7 generatorという体系そのものを基準とする。

参考: https://escholarship.mcgill.ca/downloads/5x21tf46t

したがって本仕様の本質は、

\[
\boxed{
\text{reference temperamentを拡大するのではなく、
reference temperamentを生む理論構造を再計算する}
}
\]

ことにある。
