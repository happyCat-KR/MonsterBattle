# 에셋 출처

## 이번 게임의 원본 에셋

- 지형, 성채, 성문, 탑, 나무, 바위, 깃발, 병사, 방패·창·활·검, 말과 늑대형 탈것: `DefenseArt.cs`의 절차적 형상 및 Unity 기본 도형으로 구성.
- 카드 그림: 위 모델을 별도 카메라로 RenderTexture에 촬영한 미리보기.
- 보행·공격·사망 표현: Transform을 코드로 애니메이션.
- 선택·타격·성문 피격·출전·승리 효과음: `DefenseGame.cs`에서 사인파와 노이즈로 합성한 원본 소리.
- UI: Unity uGUI로 직접 구성.

외부 에셋 스토어나 게임에서 모델·이미지·음악을 내려받지 않았다. Shieldwall의 리소스를 포함하지 않는다.

## 플랫폼 리소스

- URP Lit 셰이더, 기본 도형, uGUI, Input System: 프로젝트의 Unity 패키지 사용.
- 한글 글꼴: 실행 운영체제의 Apple SD Gothic Neo / Malgun Gothic / Noto Sans CJK KR 등을 동적으로 사용한다. 글꼴 파일을 복제하거나 앱에 동봉하지 않는다. macOS 에디터에서 표시 확인, 다른 운영체제의 글꼴 표시는 미검증.

## 참고

[Shieldwall 공식 소개](https://store.steampowered.com/app/1216320/)는 집단 전투와 성채 방어의 방향 참고용이다. 기존 `References/prototype.html`은 카드 선택과 전투 흐름의 참고 자료로 보존한다.
