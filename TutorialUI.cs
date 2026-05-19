// ======================================================================================================================================================
//
// [ チュートリアルUIの表示クラス ]
//
// 制作者: 陳 泳冲
// 日付:  2025/06/16
//
// [●] = シーンジャンプ
// [◆] = 関数毎にジャンプ
//
// ======================================================================================================================================================



// ======================================================================================================================================================
// プリプロセッサ命令 [●]
// ======================================================================================================================================================

using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ======================================================================================================================================================
// 列挙型 [●]
// ======================================================================================================================================================

// 入力デバイスの種類
public enum InputType
{
	Controller, // コントローラー入力
	Keyboard,   // キーボード入力
}

// チュートリアルUIの表示クラス
// 入力デバイス（キーボード/ゲームパッド）を検出し、適切なアイコンとテキストを表示する
// フェードイン・アウトアニメーションでチュートリアルの表示切替を行う
public class TutorialUI : MonoBehaviour
{
	// ======================================================================================================================================================
	// インスペクター設定 [◆]
	// ======================================================================================================================================================

	[SerializeField] private Image tutorialIcon;            // コントローラーアイコンの画像
	[SerializeField] private Image highLightPoint;          // ハイライトポイントの画像
	[SerializeField] private Image upDownIcon;              // 上下ボタン専用アイコンの画像
	[SerializeField] private Image tutorialBackGround;      // チュートリアルの背景画像
	[SerializeField] private TextMeshProUGUI tutorialTextMeshPro; // チュートリアルのテキスト（TextMeshPro版）

	// ======================================================================================================================================================
	// 内部状態 [◆]
	// ======================================================================================================================================================

	TutorialRenderer tr;                        // TutorialRendererへの参照

	Sprite keyboardIcon;                        // キーボード用アイコンのスプライト
	Sprite gamepadIcon;                         // ゲームパッド用アイコンのスプライト
	Sprite controllerIcon = null;               // コントローラー全体アイコンのスプライト
	bool extraIcon = false;                     // カスタムアイコンを使用するかどうか

	Vector3 gamepadIconOffset  = new Vector3(0f, 25f, 0f); // ゲームパッドアイコンのオフセット位置
	Vector3 keyboardIconOffset = new Vector3(0f, 0f, 0f);  // キーボードアイコンのオフセット位置
	Vector3 iconPos;                            // アイコンの基準位置

	float3 gamepadIconScale  = new float3(1f, 1f, 1f);    // ゲームパッドアイコンのスケール
	float3 keyboardIconScale = new float3(1f, 1f, 1f);    // キーボードアイコンのスケール

	[SerializeField] float requiredTime = 1f;   // フェードアニメーションの所要時間
	float currentTime = 1f;                     // フェードアニメーションの経過時間

	private bool isAlreadyDrawed = false;       // チュートリアルが表示中かどうか
	private bool isFinished      = false;       // フェードアニメーションが完了したかどうか

	string textGamepad;                         // ゲームパッド用テキスト
	string textKeyboard;                        // キーボード用テキスト

	// テキスト内のキーワードをTextMeshProのアイコンスプライトに置換するための辞書
	// 例: "[Aボタン]" → "<sprite=5>" のような形式に変換する
	private Dictionary<string, KeyIcon.ButtonIconType> tagToButtonIcon = new Dictionary<string, KeyIcon.ButtonIconType>
	{
		{ "Aボタン",           KeyIcon.ButtonIconType.Aw            },
		{ "Bボタン",           KeyIcon.ButtonIconType.Bw            },
		{ "Xボタン",           KeyIcon.ButtonIconType.Xw            },
		{ "Yボタン",           KeyIcon.ButtonIconType.Yw            },
		{ "Aボタン色",         KeyIcon.ButtonIconType.A             },
		{ "Bボタン色",         KeyIcon.ButtonIconType.B             },
		{ "Xボタン色",         KeyIcon.ButtonIconType.X             },
		{ "Yボタン色",         KeyIcon.ButtonIconType.Y             },
		{ "RBボタン",          KeyIcon.ButtonIconType.RB            },
		{ "LBボタン",          KeyIcon.ButtonIconType.LB            },
		{ "RTボタン",          KeyIcon.ButtonIconType.RT            },
		{ "LTボタン",          KeyIcon.ButtonIconType.LT            },
		{ "上ボタン",          KeyIcon.ButtonIconType.Up            },
		{ "下ボタン",          KeyIcon.ButtonIconType.Down          },
		{ "左ボタン",          KeyIcon.ButtonIconType.Left          },
		{ "右ボタン",          KeyIcon.ButtonIconType.Right         },
		{ "上下ボタン",        KeyIcon.ButtonIconType.UpDown        },
		{ "左右ボタン",        KeyIcon.ButtonIconType.LeftRight     },
		{ "右スティック",      KeyIcon.ButtonIconType.RightStick       },
		{ "右スティック上",    KeyIcon.ButtonIconType.RightStickUp     },
		{ "右スティック下",    KeyIcon.ButtonIconType.RightStickDown   },
		{ "右スティック左",    KeyIcon.ButtonIconType.RightStickLeft   },
		{ "右スティック右",    KeyIcon.ButtonIconType.RightStickRight  },
		{ "右スティック左右",  KeyIcon.ButtonIconType.RightStickLeftRight },
		{ "右スティック上下",  KeyIcon.ButtonIconType.RightStickUpDown   },
		{ "左スティック",      KeyIcon.ButtonIconType.LeftStick        },
		{ "左スティック上",    KeyIcon.ButtonIconType.LeftStickUp      },
		{ "左スティック下",    KeyIcon.ButtonIconType.LeftStickDown    },
		{ "左スティック左t",   KeyIcon.ButtonIconType.LeftStickLeft    },
		{ "左スティック右",    KeyIcon.ButtonIconType.LeftStickRight   },
		{ "左スティック左右",  KeyIcon.ButtonIconType.LeftStickLeftRight },
		{ "左スティック上下",  KeyIcon.ButtonIconType.LeftStickUpDown   },
		{ "オプションボタン",  KeyIcon.ButtonIconType.Option           },
		{ "WASDキー",          KeyIcon.ButtonIconType.WASD             },
		{ "Aキー",             KeyIcon.ButtonIconType.Ak               },
		{ "Sキー",             KeyIcon.ButtonIconType.Sk               },
		{ "Dキー",             KeyIcon.ButtonIconType.Dk               },
		{ "Wキー",             KeyIcon.ButtonIconType.Wk               },
		{ "Rキー",             KeyIcon.ButtonIconType.Rk               },
		{ "Eキー",             KeyIcon.ButtonIconType.Ek               },
		{ "Fキー",             KeyIcon.ButtonIconType.Fk               },
		{ "Pキー",             KeyIcon.ButtonIconType.Pk               },
		{ "Escキー",           KeyIcon.ButtonIconType.Escape           },
		{ "Spaceキー",         KeyIcon.ButtonIconType.Space            },
		{ "マウス",            KeyIcon.ButtonIconType.Mouse            },
		{ "マウス移動",        KeyIcon.ButtonIconType.MouseMove        },
		{ "マウスホイール",    KeyIcon.ButtonIconType.ScrollWheel      }
	};

	// ======================================================================================================================================================
	// 初期化 [◆]
	// ======================================================================================================================================================
	// 開始時の初期化処理
	// UI要素のアルファ値を0に設定し、チュートリアルを非表示状態にする
	void Start()
	{
		Color c;

		// チュートリアルアイコンを透明化
		c = tutorialIcon.color;
		c.a = 0.0f;
		tutorialIcon.color = c;

		// ハイライトポイントを透明化
		c = highLightPoint.color;
		c.a = 0.0f;
		highLightPoint.color = c;

		// 上下アイコンを透明化
		c = upDownIcon.color;
		c.a = 0.0f;
		upDownIcon.color = c;

		// 背景を透明化
		c = tutorialBackGround.color;
		c.a = 0.0f;
		tutorialBackGround.color = c;

		// テキストを透明化
		c = tutorialTextMeshPro.color;
		c.a = 0.0f;
		tutorialTextMeshPro.color = c;

		// タイマーを必要時間で初期化（フェード完了までの時間）
		currentTime = requiredTime;

		// 状態フラグの初期化
		isAlreadyDrawed = false;    // チュートリアル未表示状態
		isFinished      = false;    // アニメーション未完了状態
	}

	// ======================================================================================================================================================
	// 更新処理 [◆]
	// ======================================================================================================================================================
	// 毎フレーム呼ばれる更新処理
	// 入力デバイス（キーボード/ゲームパッド）を検出し、適切なUIを表示
	// フェードイン・アウトのアニメーションを制御
	private void Update()
	{
		// カメラが初期化されるまで処理をスキップ
		if (!CameraController.main.isStarted) return;

		if (isAlreadyDrawed)
		{
			// キーボード使用時の処理
			if (Gamepad.current == null)
			{
				// キーボード用テキストを設定
				tutorialTextMeshPro.text = textKeyboard;

				// ハイライトポイントを非表示（キーボードには不要）
				Color c = highLightPoint.color;
				c.a = 0f;
				highLightPoint.color = c;

				// キーボード用アイコンを設定
				tutorialIcon.sprite = keyboardIcon;
				tutorialIcon.transform.localScale = keyboardIconScale;
				tutorialIcon.transform.localPosition = iconPos + keyboardIconOffset;
			}

			// ゲームパッド使用時の処理
			else
			{
				// ゲームパッド用テキストを設定
				tutorialTextMeshPro.text = textGamepad;

				// コントローラー全体のアイコンを使用する場合
				if (controllerIcon != null)
				{
					// キーボードモードかどうかを判定
					bool isKeyboard = (tr.highlightButton == HighlightButton.None || Gamepad.current == null);

					// ゲームパッドの場合は点滅アニメーション（cos波で0〜1を往復）
					Color c = highLightPoint.color;
					c.a = (isKeyboard ? 0f : Mathf.Abs(Mathf.Cos(Time.time * Mathf.PI)));
					highLightPoint.color = c;

					// コントローラー全体のアイコンを設定
					tutorialIcon.sprite = controllerIcon;
					tutorialIcon.transform.localScale = gamepadIconScale;
					tutorialIcon.transform.localPosition = iconPos + gamepadIconOffset;
				}

				// 個別ボタンアイコンを使用する場合
				else
				{
					// ハイライトポイントのアルファ値を非表示に
					Color c = highLightPoint.color;
					c.a = 0f;
					highLightPoint.color = c;

					// ゲームパッド用アイコンを設定
					tutorialIcon.sprite = gamepadIcon;
					tutorialIcon.transform.localScale = gamepadIconScale;
					tutorialIcon.transform.localPosition = iconPos + gamepadIconOffset;
				}
			}

			// フェードインアニメーション（isFinishedでないときのみ実行）
			if (!isFinished)
			{
				currentTime += Time.deltaTime;
				float alpha = Mathf.Clamp01(currentTime / requiredTime);
				SetAlpha(alpha);

				if (currentTime >= requiredTime)
				{
					SetAlpha(1f);
					isFinished = true;
				}
			}
		}

		// 非表示処理
		else
		{
			if (!isFinished)
			{
				currentTime -= Time.deltaTime;
				float alpha = Mathf.Clamp01(currentTime / requiredTime);
				SetAlpha(alpha);

				if (currentTime <= 0f)
				{
					currentTime = 0f;
					SetAlpha(0f);
					isFinished = true;
				}
			}
		}
	}

	// ======================================================================================================================================================
	// 表示するかどうかを変更 [◆]
	// ======================================================================================================================================================
	// チュートリアルの表示/非表示を切り替える
	public void SetDisplay(bool display)
	{
		isAlreadyDrawed = display;  // チュートリアルの表示フラグを設定
		isFinished      = false;    // アニメーション完了フラグをリセット
	}

	// ======================================================================================================================================================
	// ハイライトするボタンを設置 [◆]
	// ======================================================================================================================================================
	// コントローラー上のハイライトポイントの位置を設定する
	// 特定のボタンを強調表示する際に使用する
	public void SetHighLightPoint(Vector3 position)
	{
		highLightPoint.transform.localPosition = position;
	}

	// ======================================================================================================================================================
	// 上下ボタンアイコンを設置 [◆]
	// ======================================================================================================================================================
	// 上下ボタン専用アイコンの位置を設定する
	// HighlightButton.UpDownが選択された場合に使用する
	public void SetUpDownIcon(Vector3 position)
	{
		upDownIcon.transform.localPosition = position;
	}

	// ======================================================================================================================================================
	// チュートリアルで表示するテキストを設定 [◆]
	// ======================================================================================================================================================
	// チュートリアルテキストを設定する
	// キーボード用とゲームパッド用の2種類のテキストを受け取り、
	// タグ（[Aボタン]など）をアイコンスプライトに自動置換する
	public void SetTutorialText(string textKB, string textPad, float fontSize, float iconSizeScale = 5.0f)
	{
		// nullチェックと空文字チェック
		if (tutorialTextMeshPro == null || string.IsNullOrEmpty(textKB) || string.IsNullOrEmpty(textPad)) return;

		// テキストを保存
		textGamepad  = textPad;
		textKeyboard = textKB;

		// フォントサイズを設定
		tutorialTextMeshPro.fontSize = fontSize;

		// タグをアイコンスプライトに置換
		foreach (var kvp in tagToButtonIcon)
		{
			// スプライトアセットを読み込み
			tutorialTextMeshPro.spriteAsset = Resources.Load<TMP_SpriteAsset>("Textures/UI/Icons");

			// タグキーワード（例: [Aボタン]）
			string keyword = $"[{kvp.Key}]";

			// ボタンに対応するアイコン名を取得
			string spriteName = KeyIcon.GetButtonIconName(kvp.Value);

			// アイコン名から数値インデックスを抽出
			string iconIndexString = spriteName.Replace("Icons_", "");
			int    iconIndex       = int.Parse(iconIndexString);

			// TextMeshProのスプライトタグを生成
			// "<size=500%><sprite=5></size>" のような形式
			string spriteTag = $"<size={iconSizeScale * 100}%><sprite={iconIndex}></size>";

			// テキスト内のタグをスプライトタグに置換
			textGamepad  = textGamepad.Replace(keyword, spriteTag);
			textKeyboard = textKeyboard.Replace(keyword, spriteTag);
		}
	}

	// ======================================================================================================================================================
	// アルファ値を設定 [◆]
	// ======================================================================================================================================================
	// 全てのUI要素のアルファ値を一括設定する
	// フェードイン・アウトアニメーションで使用する
	void SetAlpha(float alpha)
	{
		Color c;

		// チュートリアルアイコンのアルファ値を設定
		c = tutorialIcon.color;
		c.a = alpha;
		tutorialIcon.color = c;

		// ハイライトポイントと上下アイコンのアルファ値を設定
		// キーボード使用時やハイライトボタンが無い場合は非表示
		if (tr)
		{
			bool isKeyboard = (tr.highlightButton == HighlightButton.None || Gamepad.current == null);

			c = highLightPoint.color;
			c.a = (isKeyboard ? 0f : alpha);    // キーボードの場合は常に0
			highLightPoint.color = c;

			c = upDownIcon.color;
			c.a = (isKeyboard ? 0f : alpha);    // キーボードの場合は常に0
			upDownIcon.color = c;
		}

		// 背景のアルファ値を設定
		c = tutorialBackGround.color;
		c.a = alpha;
		tutorialBackGround.color = c;

		// テキストのアルファ値を設定
		c = tutorialTextMeshPro.color;
		c.a = alpha;
		tutorialTextMeshPro.color = c;
	}

	// ======================================================================================================================================================
	// 表示するアイコンを設定（標準アイコン） [◆]
	// ======================================================================================================================================================
	// チュートリアルアイコンを設定する（KeyIconクラスの標準アイコンを使用）
	// ゲームパッド・キーボード両方のアイコンを読み込む
	public void SetIcon(KeyIcon.ControllerIconType padIconType, KeyIcon.KeyboardIconType kbIconType,
		float padIconSize, Vector3 padPos, float kbIconSize, Vector3 kbPos, TutorialRenderer tr)
	{
		// アイコン名を取得
		string padIconName = KeyIcon.GetControllerIconName(padIconType);
		string kbIconName  = KeyIcon.GetKeyboardIconName(kbIconType);
		this.tr = tr;

		// 現在のアイコン位置を保存
		iconPos = tutorialIcon.transform.localPosition;

		// キーボードアイコンを読み込み
		Sprite[] sprite = Resources.LoadAll<Sprite>($"Textures/UI/KeyboardMouseIcon");
		keyboardIcon = sprite.FirstOrDefault(s => s.name == kbIconName);

		// ゲームパッドアイコンを読み込み
		if (padIconType != KeyIcon.ControllerIconType.Controller)
		{
			// 個別ボタンアイコン（A, B, X, Yなど）
			sprite     = Resources.LoadAll<Sprite>($"Textures/UI/XboxControllerIcon");
			gamepadIcon = sprite.FirstOrDefault(s => s.name == padIconName);
		}
		else
		{
			// コントローラー全体のアイコン
			controllerIcon = Resources.Load<Sprite>($"Textures/UI/XboxController");
		}

		// ゲームパッドアイコンのスケールとオフセットを設定
		gamepadIconScale  = new float3(padIconSize, padIconSize, padIconSize);
		gamepadIconOffset = padPos;

		// キーボードアイコンのスケールとオフセットを設定
		keyboardIconScale  = new float3(kbIconSize, kbIconSize, kbIconSize);
		keyboardIconOffset = kbPos;
	}

	// ======================================================================================================================================================
	// 表示するアイコンをカスタムスプライトで設定 [◆]
	// ======================================================================================================================================================
	// チュートリアルアイコンを設定する（任意のスプライトを使用）
	// デフォルトのアイコンシステムを使わず、カスタムスプライトを直接指定可能
	public void SetIcon(Sprite padSprite, Sprite kbSprite,
		float padIconSize, Vector3 padPos, float kbIconSize, Vector3 kbPos, TutorialRenderer tr)
	{
		this.tr = tr;

		// カスタムスプライトを直接設定
		keyboardIcon = kbSprite;
		gamepadIcon  = padSprite;

		// 現在のアイコン位置を保存
		iconPos = tutorialIcon.transform.localPosition;

		// ゲームパッドアイコンのスケールとオフセットを設定
		gamepadIconScale  = new float3(padIconSize, padIconSize, padIconSize);
		gamepadIconOffset = padPos;

		// キーボードアイコンのスケールとオフセットを設定
		keyboardIconScale  = new float3(kbIconSize, kbIconSize, kbIconSize);
		keyboardIconOffset = kbPos;

		// ハイライトポイントと上下アイコンを完全に非表示にする（カスタムアイコンでは使用しない）
		highLightPoint.color = new Color(0f, 0f, 0f, 0f);
		upDownIcon.color     = new Color(0f, 0f, 0f, 0f);

		extraIcon = true;   // カスタムアイコン使用フラグを立てる
	}
}
