// ======================================================================================================================================================
//
// [ チュートリアルUIの表示トリガー ]
//
// 制作者: 陳 泳冲
// 日付:  2025/07/05
//
// [●] = シーンジャンプ
// [◆] = 関数毎にジャンプ
//
// ======================================================================================================================================================



// ======================================================================================================================================================
// プリプロセッサ命令 [●]
// ======================================================================================================================================================

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ======================================================================================================================================================
// 列挙型 [●]
// ======================================================================================================================================================

// ハイライトするボタンの種類
public enum HighlightButton
{
	None,
	A,
	B,
	X,
	Y,
	Up,
	Down,
	Right,
	Left,
	UpDown,
	RightStick,
	RightStickUp,
	RightStickDown,
	RightStickRight,
	RightStickLeft,
	LeftStick,
	LeftStickUp,
	LeftStickDown,
	LeftStickRight,
	LeftStickLeft,
	RB,
	LB,
	RT,
	LT,
	Option
}

// チュートリアルUIの表示トリガークラス
// プレイヤーがトリガーエリアに入ると対応するチュートリアルUIを表示する
// 入力デバイス（キーボード/ゲームパッド）に応じてアイコンとテキストを切り替える
public class TutorialRenderer : MonoBehaviour
{
	// ======================================================================================================================================================
	// インスペクター設定 [◆]
	// ======================================================================================================================================================

	// ハイライトするボタンの種類
	public HighlightButton highlightButton = HighlightButton.None;

	[Header("ゲームパッドアイコン設定")]
	public KeyIcon.ControllerIconType padIconType = KeyIcon.ControllerIconType.None;    // Gamepadアイコンの種類
	[SerializeField] private float   padIconScale  = 1f;                                // アイコンのサイズ
	[SerializeField] private Vector3 padIconOffset = Vector3.zero;                      // アイコンのオフセット位置
	[SerializeField] Sprite padIconSprite;                                              // ゲームパッドアイコンのスプライト

	[Header("キーボードアイコン設定")]
	public KeyIcon.KeyboardIconType keyboardIconType = KeyIcon.KeyboardIconType.None;   // Keyboardアイコンの種類
	[SerializeField] private float   keyboardIconScale  = 1f;                           // アイコンのサイズ
	[SerializeField] private Vector3 keyboardIconOffset = Vector3.zero;                 // アイコンのオフセット位置
	[SerializeField] Sprite kbIconSprite;                                               // キーボードアイコンのスプライト

	[TextArea]
	public string displayTextKeyboard = "";     // キーボード時に表示するテキスト

	[TextArea]
	public string displayTextGamepad = "";      // ゲームパッド時に表示するテキスト

	[SerializeField] private float tutorialTextFontSize;    // テキストの文字サイズ
	[SerializeField] private float textIconSizeScale = 0.7f; // テキストで表示するアイコンのサイズ

	[SerializeField] TutorialUI tutorialUI; // チュートリアルUIのインスタンス

	// ======================================================================================================================================================
	// 定数・静的メンバー [◆]
	// ======================================================================================================================================================

	// ハイライトを画面外に飛ばして非表示にするための座標
	private static readonly Vector3 HiddenPosition = Vector3.one * 9999f;

	// ボタン位置の基準解像度
	private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

	// 1920x1080基準のコントローラー各ボタン位置（Canvas中心からのオフセット）
	private static class ButtonPos
	{
		// フェイスボタン（右側）
		public static readonly Vector2 A = new Vector2(-497, -363);
		public static readonly Vector2 B = new Vector2(-478, -344);
		public static readonly Vector2 X = new Vector2(-516, -344);
		public static readonly Vector2 Y = new Vector2(-497, -326);

		// D-Pad（左側）
		public static readonly Vector2 DPadUp     = new Vector2(-607, -370);
		public static readonly Vector2 DPadDown   = new Vector2(-607, -406);
		public static readonly Vector2 DPadRight  = new Vector2(-587, -388);
		public static readonly Vector2 DPadLeft   = new Vector2(-625, -388);
		public static readonly Vector2 DPadUpDown = new Vector2(-607, -388);

		// 右スティック
		public static readonly Vector2 RightStick      = new Vector2(-533, -387);
		public static readonly Vector2 RightStickUp    = new Vector2(-533, -368);
		public static readonly Vector2 RightStickDown  = new Vector2(-533, -405);
		public static readonly Vector2 RightStickRight = new Vector2(-515, -387);
		public static readonly Vector2 RightStickLeft  = new Vector2(-553, -387);

		// 左スティック
		public static readonly Vector2 LeftStick      = new Vector2(-642, -344);
		public static readonly Vector2 LeftStickUp    = new Vector2(-642, -326);
		public static readonly Vector2 LeftStickDown  = new Vector2(-642, -362);
		public static readonly Vector2 LeftStickRight = new Vector2(-624, -344);
		public static readonly Vector2 LeftStickLeft  = new Vector2(-660, -344);
	}

	// HighlightButtonとボタン座標のマッピングテーブル
	private static readonly Dictionary<HighlightButton, Vector2> HighlightPositions = new Dictionary<HighlightButton, Vector2>
	{
		{ HighlightButton.A,               ButtonPos.A               },
		{ HighlightButton.B,               ButtonPos.B               },
		{ HighlightButton.X,               ButtonPos.X               },
		{ HighlightButton.Y,               ButtonPos.Y               },
		{ HighlightButton.Up,              ButtonPos.DPadUp          },
		{ HighlightButton.Down,            ButtonPos.DPadDown        },
		{ HighlightButton.Right,           ButtonPos.DPadRight       },
		{ HighlightButton.Left,            ButtonPos.DPadLeft        },
		{ HighlightButton.UpDown,          ButtonPos.DPadUpDown      },
		{ HighlightButton.RightStick,      ButtonPos.RightStick      },
		{ HighlightButton.RightStickUp,    ButtonPos.RightStickUp    },
		{ HighlightButton.RightStickDown,  ButtonPos.RightStickDown  },
		{ HighlightButton.RightStickRight, ButtonPos.RightStickRight },
		{ HighlightButton.RightStickLeft,  ButtonPos.RightStickLeft  },
		{ HighlightButton.LeftStick,       ButtonPos.LeftStick       },
		{ HighlightButton.LeftStickUp,     ButtonPos.LeftStickUp     },
		{ HighlightButton.LeftStickDown,   ButtonPos.LeftStickDown   },
		{ HighlightButton.LeftStickRight,  ButtonPos.LeftStickRight  },
		{ HighlightButton.LeftStickLeft,   ButtonPos.LeftStickLeft   },
	};

	// ======================================================================================================================================================
	// ユーティリティ関数 [◆]
	// ======================================================================================================================================================
	// Canvasの実際のサイズからハイライト座標をスケーリングする
	// CanvasScalerのモードに依存しない汎用的な方法
	private Vector3 GetScaledHighlightPos(Vector2 refPos)
	{
		RectTransform canvasRect = tutorialUI.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
		if (canvasRect == null) return refPos;

		// 実際の解像度と基準解像度の比率を計算
		float scaleX = canvasRect.rect.width  / ReferenceResolution.x;
		float scaleY = canvasRect.rect.height / ReferenceResolution.y;
		return new Vector3(refPos.x * scaleX, refPos.y * scaleY, 0f);
	}

	// ======================================================================================================================================================
	// 初期化 [◆]
	// ======================================================================================================================================================
	// 開始時にテキスト・ハイライト座標・アイコンを設定する
	private void Start()
	{
		// テキスト初期設定
		tutorialUI.SetTutorialText(displayTextKeyboard, displayTextGamepad, tutorialTextFontSize, textIconSizeScale);

		// HighlightPositionsテーブルからボタン座標を取得してハイライトを設定
		if (HighlightPositions.TryGetValue(highlightButton, out Vector2 refPos))
		{
			Vector3 pos = GetScaledHighlightPos(refPos) + padIconOffset;

			// 上下ボタンの場合は専用アイコンを使用
			if (highlightButton == HighlightButton.UpDown)
			{
				tutorialUI.SetUpDownIcon(pos);
				tutorialUI.SetHighLightPoint(HiddenPosition);
			}
			else
			{
				tutorialUI.SetHighLightPoint(pos);
				tutorialUI.SetUpDownIcon(HiddenPosition);
			}
		}
		else
		{
			// テーブルに存在しないボタンは非表示
			tutorialUI.SetHighLightPoint(HiddenPosition);
			tutorialUI.SetUpDownIcon(HiddenPosition);
		}

		// アイコン設定（カスタムスプライトが両方指定されている場合はそちらを優先）
		if (padIconSprite != null && kbIconSprite != null)
		{
			tutorialUI.SetIcon(padIconSprite, kbIconSprite, padIconScale, padIconOffset, keyboardIconScale, keyboardIconOffset, this);
		}
		else
		{
			tutorialUI.SetIcon(padIconType, keyboardIconType, padIconScale, padIconOffset, keyboardIconScale, keyboardIconOffset, this);
		}
	}

	// ======================================================================================================================================================
	// エリア内に入るとチュートリアルを表示する [◆]
	// ======================================================================================================================================================
	// プレイヤーがトリガーエリアに入った時にチュートリアルを表示する
	private void OnTriggerEnter(Collider other)
	{
		if (other.gameObject == Games.stage.mainPlayer.gameObject)
		{
			tutorialUI.SetDisplay(true);
		}
	}

	// ======================================================================================================================================================
	// エリアから出るとチュートリアルを非表示にする [◆]
	// ======================================================================================================================================================
	// プレイヤーがトリガーエリアから出た時にチュートリアルを非表示にする
	private void OnTriggerExit(Collider other)
	{
		if (other.gameObject == Games.stage.mainPlayer.gameObject)
		{
			tutorialUI.SetDisplay(false);
		}
	}
}
