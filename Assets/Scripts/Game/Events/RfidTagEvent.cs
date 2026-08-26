namespace DGAIZone.Game.Events
{
    /// <summary>
    /// RFID 카드가 인식되었을 때 MessagePipe를 통해 발행되는 이벤트 구조체.
    /// </summary>
    public readonly struct RfidTagEvent
    {
        public readonly string ReaderId;
        public readonly string Category;
        public readonly string IngredientName;
        public readonly string[] MatterNames;

        /// <summary>
        /// 인식된 리더기 ID, 카드 분류, 재료 이름, 세부 물질 목록을 초기화함.
        /// </summary>
        public RfidTagEvent(string readerId, string category, string ingredientName, string[] matterNames)
        {
            ReaderId = readerId;
            Category = category;
            IngredientName = ingredientName;
            MatterNames = matterNames;
        }
    }
}
