using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using HuliacDev.Core;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// IngredientSelectionController의 레벨별(1~5) 동작 및 판정 상태를 추상화하는 인터페이스.
    /// </summary>
    public interface IIngredientSelectionLevelState : IState<IngredientSelectionController>
    {
        /// <summary> 수신된 RFID 태그 카테고리가 해당 레벨 규칙에 맞는지 검증. </summary>
        bool ValidateTagCategory(IngredientSelectionController controller, RfidTagEvent evt);

        /// <summary> 해당 단계에서 리더기가 안내할 허용 카테고리 목록을 반환. </summary>
        string[] GetAllowedCategories(IngredientSelectionController controller, RfidStepDefinition step);

        /// <summary> 스캔된 카드 정보로부터 실제 사용할 재료(id, 이름, 물질 목록)를 해석. 해석할 수 없으면 null. </summary>
        RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category);

        /// <summary> 물질 선택 목록 중 이미 확정된 값을 제외할지 여부를 결정하여 반환. </summary>
        RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters);

        /// <summary> 단계 확정 시 레벨별 효과 및 값을 적용. </summary>
        void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter);

        /// <summary> 단계 취소(되돌리기) 시 적용되었던 레벨별 효과를 원복. </summary>
        void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter);

        /// <summary> 카드가 떨어지거나 돌아와 설계창 블록을 임시로 떨어뜨리거나 다시 붙인 뒤 호출됨(워크플로우 초기화·취소·되돌리기 뒤에도 호출됨). </summary>
        void OnMissingCardsRefreshed(IngredientSelectionController controller);

        /// <summary> 설계창에 값 블록(명령 블록 오른쪽에 끼우는 블록)이 쌓일 수 있는 레벨인지 여부. 설계창이 블록 묶음이 화면 폭을 넘지 않는 배율과 블록이 붙는 연출 시간을 정하는 데 씀. </summary>
        bool UsesValueBlocks { get; }

        /// <summary> 함수 사용 단계가 있어 설계창 오른쪽에 함수 정의 블록 자리를 남겨야 하는지 여부. </summary>
        bool UsesFunctionDefinition { get; }

        /// <summary> 설계창 블록에 쓸 문구(명령 블록 문구, 값 블록 문구)를 반환. 값 문구가 null이면 값 블록 없는 명령 블록으로 쌓임. </summary>
        (string command, string value) GetDesignBlockTexts(IngredientSelectionController controller, string ingredientName, string matterLabel);

        /// <summary>
        /// 재료(ingredientId)를 설계창에 쌓을 블록 모양(명령, ㄷ자, ㄷ자 안쪽, 논리, 함수 사용)을 반환. 바로 앞 단계의 재료(previousIngredientId,
        /// 첫 단계면 null)에 따라 달라질 수 있음(레벨 4: 반복하기 바로 뒤 이동하기는 ㄷ자 안쪽). 게임 중 확정과 결과 씬 정답 설계에 함께 쓰임.
        /// </summary>
        DesignStepShape GetDesignStepShape(IngredientSelectionController controller, string ingredientId, string previousIngredientId);

        /// <summary> 코딩완료 버튼의 활성화 가능 여부를 반환. </summary>
        bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps);

        /// <summary> 미션 성공/실패 여부를 판정. </summary>
        bool EvaluateMission(IngredientSelectionController controller);

        /// <summary>
        /// 실패했을 때 실패 원인별 결과 영상이 있으면 그 접미사("4-{레벨}-Fail-{접미사}.mp4")를, 없으면 null(기본 실패 영상)을 반환.
        /// 코딩완료 판정 뒤 실패일 때만 불림.
        /// </summary>
        string GetFailVideoSuffix(IngredientSelectionController controller);

        /// <summary>
        /// 이번 판 문제(레벨 1 목적지, 레벨 3 기준값, 레벨 4 보드 배치 등)를 성공시키는 정답 블록을 입력 순서대로 반환.
        /// 결과 씬의 AI 설계창에 쓰이며, 정답을 만들 수 없으면 경고를 남기고 빈 목록을 반환.
        /// </summary>
        List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller);

        /// <summary> 확정된 값으로 계산한 추진력(레벨 1 전용, 다른 레벨은 0). 미션 보드 진행도에 쓰임. </summary>
        int CalculateConfirmedThrust(IngredientSelectionController controller);

        /// <summary> 임시 선택값을 반영한 추진력 미리보기 계산(레벨 1 전용). </summary>
        int CalculatePreviewThrust(IngredientSelectionController controller);

        /// <summary> 코딩 완료 후 결과 씬 전환 전 실행할 레벨별 추가 비동기 연출. </summary>
        UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token);
    }
}
