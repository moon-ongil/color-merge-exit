# Color Exit — 에셋 요청 시트 (GPT용)

블록 퍼즐 버전에 필요한 스프라이트를 **한 장의 스프라이트 시트**로 요청하기 위한 정리.
> 효율을 위해 **모두 한 이미지(1 sheet)** 에 격자로 촘촘히 배치 요청. 배경 **투명(알파)**, 스타일 통일.

## 공통 스타일
- **탑다운(위에서 본) 뷰**, 캐주얼 모바일 퍼즐 톤, **광택(glossy)/젤리감**, 부드러운 그림자, 굵고 깔끔한 외곽.
- **배경 완전 투명 PNG** (체커/흰색 배경 금지 — 진짜 알파).
- 각 스프라이트는 **정사각 셀 안에 여백을 두고 중앙 정렬**, 셀 간 **뚜렷한 간격**(자동 슬라이스 되게).
- 해상도 권장: **1536×1536** 이상, 한 셀 ≈ 240–256px.

## 색상 팔레트 (정확히 맞춰주세요)
| 이름 | HEX |
|---|---|
| Red | `#E63D3D` |
| Blue | `#337AEB` |
| Yellow | `#FACC2E` |
| Green | `#47BD59` |
| Purple | `#9E59D4` |
| Orange | `#F28C29` |

## 시트 레이아웃 (6열 격자, 행마다 한 종류)

**Row 1 — 컬러 블록(핵심 조각)** · 6칸
- 둥근 모서리 정사각 **젤리/보석 블록**, 위쪽에 하이라이트. 위 6색 각 1개.
- 단색이지만 입체감 있게(살짝 볼록). 숫자는 넣지 말 것(게임에서 얹음).

**Row 2 — 컬러 출구 포털** · 6칸
- 보드 가장자리에 놓일 **빛나는 네온 테두리 포털/게이트**. 위 6색 각 1개.
- 가운데는 살짝 비거나 어둡게, 테두리에서 색 발광.

**Row 3 — 이동 장애물 블록(중립)** · 6칸
- 밀 수 있는 **회색 계열 블록들**: 돌(stone), 금속(metal), 나무상자(wood crate), 콘크리트, 자물쇠 블록, 타이어더미 — 각 1x1 정사각 톤.

**Row 4 — 고정 벽/장애물(맵 구조)** · 6칸
- 절대 안 움직이는 **벽/바위/기둥/코ン/화분/드럼통** 타일 (미로 벽으로 사용).

**Row 5 — 보드/배경 타일** · 6칸
- 빈 칸 타일(어두운 슬롯) 2종, 보드 프레임 모서리/변 조각, 은은한 배경 패턴 1–2종.

**Row 6 — UI/이펙트(선택)** · 6칸
- pause, next(▶▶), close(✕), sound on/off, 별(star), 작은 폭죽/반짝임 파티클.

> 총 ≈ 36칸(6×6). 칸이 남으면 비워도 됨. 필요 없으면 Row 5–6은 생략 가능.

## 이미 있어서 **다시 안 만들어도 되는 것**
- 원형 UI 버튼(home/restart/undo/settings/star 등) — 보유
- 장식용 오브젝트(콘/바리케이드/화분/타이어/덤프스터 등) — 보유
- 빨강/파랑/초록/노랑 네온 출구 프레임 — 보유 (**보라·주황만 추가로 필요** → Row 2에서 채움)

## GPT에 붙여넣을 프롬프트(예시)
```
A single game sprite sheet, transparent background (real alpha, NOT a checkerboard),
top-down casual mobile puzzle style, glossy/jelly look with soft shadows and clean
bold outlines. Arrange items in a neat 6-column grid with clear spacing between cells,
each item centered with padding so it can be auto-sliced. 1536x1536.

Row 1: six rounded-square glossy jelly BLOCKS, one each in exact colors
red #E63D3D, blue #337AEB, yellow #FACC2E, green #47BD59, purple #9E59D4, orange #F28C29
(no numbers, subtle top highlight).
Row 2: six glowing neon EXIT PORTAL frames in the same six colors (edge glow, dark center).
Row 3: six neutral grey pushable obstacle blocks (stone, metal, wooden crate, concrete,
locked block, tire stack), square footprint.
Row 4: six fixed wall/obstacle tiles (wall, rock, pillar, cone, planter, barrel).
Keep every sprite inside its own cell, no text, no background.
```

## 참고: 사이즈/모양 규칙(게임 로직)
- **타깃 블록은 1x1**(정사각). 여러 색이 겹쳐 있으면(멀티티어) 게임이 숫자로 남은 겹 수를 표시.
- **이동 장애물**은 1x1, **1x2 / 2x1 / 2x2** 등 직사각도 가능 → 블록 시트는 1x1 기준, 큰 조각은 코드에서 타일링/스케일로 처리 가능(원하면 2x1·2x2 전용도 요청 가능).
