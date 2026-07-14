# Color Merge Exit

색이 바뀌는 출구에 맞춰 **색 블록을 밀어 빼내는** 시간제 슬라이딩 퍼즐 (모바일, Unity).
얽힌 블록들을 상하좌우로 치우며 목표 블록의 길을 트는 **클로츠키 / 언블럭미** 계열 + 색 레이어 반전 트위스트.

- 그리드 기반 (물리엔진 X), 고정 맵 + 제한 시간 + 반복 학습형 기억 퍼즐
- **1×1 목표 블록**은 색 스택(2~4단)을 가짐. 매칭 색 출구로 내보내면 맨 위 색이 **벗겨지고(3→2)** 다음 색으로; 마지막 층까지 벗기면 블록이 완전히 탈출
- 보드는 다양한 크기(1×1·2×1·1×2·2×2)의 **중립 이동 블록으로 빽빽** — 이걸 치워가며 목표의 경로를 확보 (얽힘 = 난이도의 핵심)
- 목표 블록은 **전 방향 자유 이동**(어떤 출구로도 라우팅). 후반(13레벨~)에는 **한 축으로만 미끄러지는 "레일" 블록**이 섞여 러시아워형 잼 형성
- 제한 시간 안에 목표를 모두 탈출시키면 클리어, 남은 시간으로 별 1~3개

## 코어 구조

```
Assets/
  Scripts/Core/              # 순수 C# 코어 (UnityEngine 비의존, noEngineReferences → dotnet 테스트)
    Enums.cs                 # CarColor / MoveAxis / Edge / MoveResult
    GridPos.cs               # 정수 그리드 좌표 (origin top-left)
    Block.cs                 # 색 블록 (색 스택/크기/축잠금/위치) — Peel()/Tiers/CurrentColor/NextColor/CanMove
    Exit.cs                  # 색 출구 (edge/lane/색 시퀀스, 독립 진행)
    Board.cs                 # 4방향 이동/충돌/탈출·필/되돌리기 엔진 (전부-아니면-취소 칸 단위 이동)
    GameSession.cs           # 타이머 + 승/패 + 별 평가 래퍼
    LevelData.cs             # 직렬화 레벨 정의 + BuildBoard()
    ColorMergeExit.Core.asmdef
  Scripts/Game/              # Unity 렌더링/입력 레이어 (MonoBehaviour, 월드 스페이스 스프라이트)
    VisualAssets.cs          # 런타임 생성 스프라이트(글로시 블록/화살표/사각) + 색상
    BoardView.cs             # 그리드↔월드 매핑 + 블록/보더/레일/출구 렌더
    HudView.cs               # 스테이지 헤더 + 타이머 + 타임바 + 버튼 (EventSystem 비의존)
    GameController.cs        # 세션 소유 + 4방향 드래그(마우스+터치) + 타이머 + 승패
    GameSprites.cs           # 시트 스프라이트 슬롯 (블록/출구/타일/UI)
    LevelRepository.cs       # JSON 로드 + 인코드 폴백 레벨
    Bootstrap.cs             # 씬 진입점: 스테이지 선택 → 플레이
    ColorMergeExit.Game.asmdef
  Scripts/Editor/            # 에디터 전용 (배치 자동화)
    AssetSetup.cs            # 단일 스프라이트 시트 6×6 격자 슬라이스 + GameSprites 배선
    FontSetup.cs / Screenshot.cs / BuildScript.cs / SceneBuilder.cs
  Art/Textures/blocks_sheet.png   # GPT 생성 단일 시트 (블록/출구/장애/타일/UI)
  StreamingAssets/Levels/level_001..030.json
  Tests/EditMode/BoardTests.cs    # NUnit (Unity Test Runner + dotnet 공용)

dev/
  blockgen.py                # BFS 솔버 + 클로츠키 레벨 생성기 (밀도·티어·레일·시간 튜닝)
  build_ios_sim.sh           # iOS 시뮬레이터 원커맨드 빌드+배포
  CoreTests/                 # 로컬 dotnet 테스트 하니스 (Assets 밖, Unity 무시)
```

## 규칙 요약

- 원점 좌상단, x→우측, y→아래. 블록 앵커 = 최소(x,y) 셀, W×H 사각형.
- **이동은 한 번에 한 축**, 정확한 칸 단위: 요청한 만큼 전부 이동(Moved) / 매칭 출구로 필·탈출(Peeled·Exited) / 막히면 전체 취소(Blocked). 부분 이동 없음.
- **탈출/필 조건**: 1×1 목표 블록이 보드 밖으로 나가는 방향·차선의 출구가 있고 **출구 현재색 == 블록 맨 위 색**일 때. 성공 시 그 층을 벗기고(Peeled) 출구 색 시퀀스는 다음으로 진행. 마지막 층이면 블록 제거(Exited).
- **큰 블록(비-1×1)**은 절대 탈출하지 않는 이동 장애물.
- **레일 블록**(`axis` ≠ Free): 지정 축으로만 슬라이드. 목표 블록은 항상 Free.
- 승리: 목표(isTarget) 블록이 모두 탈출.

## 레벨 JSON 포맷 (enum은 정수)

```jsonc
{
  "id": 25, "name": "Stage 25", "width": 6, "height": 6,
  "blocks": [
    { "id": 1, "colors": [3,5,2,0], "x": 2, "y": 1, "w": 1, "h": 1, "isTarget": true },   // 4단 목표
    { "id": 2, "color": 0, "x": 0, "y": 0, "w": 2, "h": 1, "isTarget": false, "axis": 1 }  // 가로 레일 블록
  ],
  "exits": [ { "edge": 2, "lane": 1, "colorSequence": [3,5,2,0] } ],
  "timeLimitSeconds": 100, "star2SecondsLeft": 30, "star3SecondsLeft": 50
}
```

- `color`/`colors`: Red=0, Blue=1, Yellow=2, Green=3, Purple=4, Orange=5 (colors는 top-first 스택)
- `axis`: Free=0, Horizontal=1, Vertical=2 (레일 잠금)
- `edge`: Top=0, Bottom=1, Left=2, Right=3 · `lane`: 좌/우는 row(y), 상/하는 col(x)

## 레벨 생성 (dev/blockgen.py)

BFS 솔버가 C# `Board`와 동일 규칙으로 **풀림 여부 + 최소 이동수**를 계산. 난이도는 **밀도(빈칸 2~4)** + **색 티어** + **레일 잠금**에서 나오며, 시간은 이동수에 비례해 자동 배정(별 기준은 시간의 30%/50%).

```bash
python3 dev/blockgen.py    # Assets/StreamingAssets/Levels/level_001..030.json 재생성
```

난이도 곡선(30레벨): 1~6 순수 클로츠키(1~2단), 7~12 4색 2단, 13~18 레일 도입(2~3단), 19~24 3단, 25~30 6색 3~4단. 이동수 대략 2 → 30+.

> 밀도가 높을수록 오히려 BFS가 가벼움(짧은 슬라이드·낮은 분기) — 15-퍼즐이 빈칸 하나라 어렵지만 계산은 유한한 것과 같은 원리.

## 실행 (Unity)

1. Unity Hub에서 이 폴더 열기 (`ProjectVersion.txt` = 6000.0.78f1 LTS).
2. `Assets/Scenes/Main.unity` → **Play** (문제 시 **Tools ▸ Color Merge Exit ▸ Rebuild Main Scene**).
3. 조작: 블록을 **드래그**(상하좌우) → 매칭 색 출구 밖으로 밀면 필/탈출. 하단 **HOME / RESTART / UNDO**. 시작 시 출구 색 순서 미리보기(MEMORIZE).

> 입력은 레거시 `Input`(마우스+터치). 예외 시 Player Settings ▸ Active Input Handling → **Both**.

## 아트 파이프라인

단일 시트 `Assets/Art/Textures/blocks_sheet.png`(투명 배경, 6열×6행: 컬러블록/출구포털/이동장애/고정벽/타일/UI)를 **`Tools ▸ Color Merge Exit ▸ Set Up Art`** 로 처리:

- 6×6 **고정 격자 슬라이스** + 셀별 알파 트림 → (행,열) 위치 기준 **결정적 배선**(색 추측 없음)으로 `GameSprites` 채움
- 목표 블록 = 시트의 컬러 젤리블록(평평 탑다운), 이동 블록 = 런타임 생성 **중립 글로시 블록**(밀 수 있게 보이도록), 레일 블록 = 방향 화살표 오버레이
- HUD 아이콘(restart/undo/home)은 레거시 `ui_buttons.png`에서 색상분류로 배선

배치: `Unity -batchmode -quit -executeMethod ColorMergeExit.Editor.AssetSetup.Run`

## 폰트 / 다국어

- 키 기반 `Localization.cs`: En/Ko/Ja/ZhHans/Es/PtBr/De/Fr/It/Ru/Ar
- TMP: **Pretendard SDF** 메인 + **Noto SC/JP/Arabic** 폴백. **아랍어 RTL 셰이핑**(ArabicSupport, MIT)
- `FontSetup.OptimizeFonts`로 사용 글자만 static 아틀라스 서브셋 + TTF 참조 제거 → 빌드에서 CJK TTF(~30MB) 제외

## 테스트

```bash
cd dev/CoreTests && dotnet test    # Unity 없이 코어 검증 (net10.0), 현재 10/10
```
Unity: `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` (동일 `BoardTests.cs`).

## 모바일 빌드 — iOS 시뮬레이터

```bash
bash dev/build_ios_sim.sh
```
Unity가 Xcode 프로젝트 생성(`BuildScript.BuildIOSSimulator`, IL2CPP, SimulatorSDK) → **시뮬레이터 런타임 라이브러리를 universal로 교체**(Unity가 x86_64 전용을 복사하는 quirk 우회: `libiPhone-lib.dylib`/`baselib.a` → `*-sim-x64arm64`) → `xcodebuild`(iphonesimulator, arm64, 서명 불필요) → `simctl install/launch`.

> 실기(device) 빌드는 Player Settings에서 SDK를 Device로 바꾸고 서명 설정 필요. StreamingAssets를 Android에서 읽으려면 `UnityWebRequest` 경로 추가(현재 인코드 폴백으로 항상 플레이 가능).

## 다음 단계

- 라이선스 음원 교체(현재 BGM/SFX는 `dev/genaudio.py` 절차적 생성)
- 기록 강화(총 별 집계, "올 별3" 보상), 실패 후 힌트, 리더보드
- Android/iOS device 빌드
