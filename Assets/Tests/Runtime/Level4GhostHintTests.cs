using System.Collections;
using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 4 고스트 로봇 경로(정답 경로를 시작 칸 다음부터 기지까지 한 칸씩 펼침)와 설정 보드 아우라 켜기·끄기 검증.
    /// </summary>
    public class Level4GhostHintTests
    {
        private const string Up = Constants.RfidIds.Level4.MoveUp;
        private const string Down = Constants.RfidIds.Level4.MoveDown;
        private const string Right = Constants.RfidIds.Level4.MoveRight;
        private const string Left = Constants.RfidIds.Level4.MoveLeft;

        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        /// <summary> 구간마다 칸 수만큼 한 칸씩 펼치고, 시선은 마지막 좌우 이동을 따름(위아래 이동은 그대로). </summary>
        [Test]
        public void 정답_경로를_시작_칸_다음부터_한_칸씩_펼친다()
        {
            List<(int moves, string directionId)> solution = new List<(int moves, string directionId)>
            {
                (2, Down), (3, Right), (1, Up), (1, Left)
            };

            List<Level4GhostHint.GhostCell> cells = Level4GhostHint.BuildGhostPath(solution, 1);

            Assert.IsNotNull(cells);
            (int column, int row, bool faceLeft)[] expected =
            {
                (0, 2, false), (0, 3, false),               // 아래로 2칸 — 처음 시선(오른쪽) 그대로
                (1, 3, false), (2, 3, false), (3, 3, false), // 오른쪽으로 3칸
                (3, 2, false),                               // 위로 1칸 — 오른쪽 시선 그대로
                (2, 2, true)                                 // 왼쪽으로 1칸
            };
            Assert.AreEqual(expected.Length, cells.Count, "칸 수 = 구간 칸 수의 합");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].column, cells[i].Column, $"{i + 1}번째 칸 열");
                Assert.AreEqual(expected[i].row, cells[i].Row, $"{i + 1}번째 칸 행");
                Assert.AreEqual(expected[i].faceLeft, cells[i].FaceLeft, $"{i + 1}번째 칸 시선");
            }
        }

        /// <summary> 정답 경로가 없거나 알 수 없는 방향이 섞이면 고스트 경로를 만들지 않음. </summary>
        [Test]
        public void 정답_경로가_없거나_방향을_모르면_null()
        {
            Assert.IsNull(Level4GhostHint.BuildGhostPath(null, 0));
            Assert.IsNull(Level4GhostHint.BuildGhostPath(new List<(int moves, string directionId)> { (1, Right), (1, "Jump") }, 0));
        }

        /// <summary> 아우라는 Play로 켜 알파 1까지 밝아지고 Stop으로 끄면 알파 0까지 사라짐. </summary>
        [UnityTest]
        public IEnumerator 아우라는_Play로_켜고_Stop으로_끈다()
        {
            _go = new GameObject("Image_SettingBoardAura", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            SettingBoardAura aura = _go.AddComponent<SettingBoardAura>();
            CanvasGroup group = _go.GetComponent<CanvasGroup>();
            yield return null; // Awake·Start

            Assert.AreEqual(0f, group.alpha, "처음에는 숨김");
            Assert.IsFalse(aura.IsPlaying);

            aura.Play();
            Assert.IsTrue(aura.IsPlaying);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (group.alpha < 0.99f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(group.alpha, 0.99f, "켜지면 알파 1까지 밝아짐");

            aura.Stop();
            Assert.IsFalse(aura.IsPlaying);

            deadline = Time.realtimeSinceStartup + 3f;
            while (group.alpha > 0.01f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(group.alpha, 0.01f, "끄면 알파 0까지 사라짐");
        }
    }
}
