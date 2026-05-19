// ======================================================================================================================================================
//
// [ ステージ開始時の視線誘導 ]
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

// ステージ開始時のカメラワーク制御
// Hermite曲線を使用してスタート地点からプレイヤー位置までカメラを滑らかに移動させる
public class StageStartCameraWork : MonoBehaviour
{
	// ======================================================================================================================================================
	// インスペクター設定 [◆]
	// ======================================================================================================================================================

	[Header("制御点（2点以上）")]
	[Tooltip("カメラパスの制御点リスト。最低2点必要")]
	public List<Transform> controlPoints = new List<Transform>();

	[Header("注視対象（ゴールとプレイヤー）")]
	[Tooltip("カメラの初期注視点（通常はゴール地点）")]
	public Transform lookTargetStart;

	[Tooltip("カメラの最終注視点（通常はプレイヤー）")]
	public Transform lookTargetEnd;

	[Header("全体の移動時間（秒）")]
	[Tooltip("カメラワーク全体にかかる時間")]
	public float duration = 5f;

	[Header("イーズ係数（0〜1）")]
	[Range(0f, 1f)]
	[Tooltip("Hermite曲線の接線ベクトルの強さ。大きいほど曲線が緩やかになる")]
	public float easeFactor = 0.5f;

	// ======================================================================================================================================================
	// 内部状態 [◆]
	// ======================================================================================================================================================

	// 経過時間タイマー
	float timer = 0f;

	// カメラが移動中かどうかのフラグ
	[HideInInspector] public bool isMoving { get; private set; } = false;

	// イージング設定（In/Out/InOut）
	[SerializeField] private Ease.IO easeIO = Ease.IO.InOut;

	// イージングタイプ（Back/Cubic/Quadなど）
	[SerializeField] private Ease.Type easeType = Ease.Type.Back;

	// カメラコントローラーへの参照
	CameraController cameCon;

	// デフォルトの曲線分割数
	[SerializeField] private int defaultSteps = 100;

	// ======================================================================================================================================================
	// 初期化 [◆]
	// ======================================================================================================================================================
	// 開始時の初期設定
	// カメラ位置の初期化と入力制限を行う
	void Start()
	{
		// null参照の制御点をリストから削除
		CleanUpControlPoints();

		// カメラコントローラーの参照を取得
		cameCon = CameraController.main;

		// カメラの初期位置を設定（スタート地点 + オフセット）
		transform.position = lookTargetStart.position + (PlayerSpawner.main.transform.rotation * cameCon.offset);
		isMoving = false;

		// 初期注視点を向く
		if (lookTargetStart != null)
			transform.LookAt(lookTargetStart.position);

		// カメラワーク中は入力を無効化
		InputPermission.move  = false;  // 移動入力を無効化
		InputPermission.pause = false;  // ポーズ入力を無効化
	}

	// ======================================================================================================================================================
	// 更新処理 [◆]
	// ======================================================================================================================================================
	// 毎フレーム呼ばれる更新処理
	// カメラの位置と注視点を補間する
	void Update()
	{
		// 移動中でなければ何もしない
		if (!isMoving) return;

		// 経過時間を加算
		timer += Time.unscaledDeltaTime;

		// 進行度を0〜1に正規化
		float t = Mathf.Clamp01(timer / duration);

		// イージング関数を適用して滑らかな加減速を実現
		float easedT = (float)Ease.Easing(t, easeIO, easeType);

		// Hermite曲線上の位置を計算し、カメラオフセットを加算
		transform.position = GetHermitePathPosition(easedT) +
			(Games.stage.mainPlayer.spawner.transform.rotation * cameCon.offset);

		// 注視点の補間（スタート地点からプレイヤーへ徐々に視線を移動）
		if (lookTargetStart != null && lookTargetEnd != null)
		{
			Vector3 lookPos = Vector3.Lerp(
				lookTargetStart.position,   // 開始時の注視点
				lookTargetEnd.position,     // 終了時の注視点
				easedT                      // イージング適用後の進行度
			);
			transform.LookAt(lookPos);
		}

		// カメラワーク完了時の処理
		if (t >= 1f)
		{
			// タイムスケールを通常に戻す
			Time.timeScale = 1f;

			// 移動完了フラグを設定
			isMoving = false;

			// プレイヤーの入力を有効化
			InputPermission.pause  = true;  // ポーズ可能に
			InputPermission.move   = true;  // 移動可能に
			InputPermission.camera = true;  // カメラ操作可能に
			InputPermission.recall = true;  // リコール可能に

			// カメラコントローラーを初期化済み状態に設定
			cameCon.SetInitialized();
		}
	}

	// ======================================================================================================================================================
	// Hermite曲線計算 [◆]
	// ======================================================================================================================================================
	// Hermite曲線上の位置を計算
	// 2つの制御点とその接線ベクトルから滑らかな曲線を生成
	Vector3 GetHermitePathPosition(float t)
	{
		// 制御点が2点未満の場合は現在位置を返す
		if (controlPoints.Count < 2) return transform.position;

		// 制御点を取得
		Transform p0 = controlPoints[0];
		Transform p1 = controlPoints[1];

		// 接線ベクトルを計算（制御点と注視点の差分にイーズ係数を適用）
		Vector3 m1 = (p0.position - lookTargetStart.position) * easeFactor;
		Vector3 m2 = (p1.position - lookTargetEnd.position)   * easeFactor;

		// Hermite基底関数の計算用にt^2とt^3を事前計算
		float t2 = t * t;
		float t3 = t2 * t;

		// Hermite曲線の公式
		// H(t) = (2t^3 - 3t^2 + 1)P0 + (t^3 - 2t^2 + t)M1 + (-2t^3 + 3t^2)P1 + (t^3 - t^2)M2
		return
			(2f * t3 - 3f * t2 + 1f) * lookTargetStart.position +  // P0項（開始点）
			(t3 - 2f * t2 + t)        * m1 +                        // M1項（開始接線）
			(-2f * t3 + 3f * t2)      * lookTargetEnd.position +    // P1項（終了点）
			(t3 - t2)                 * m2;                         // M2項（終了接線）
	}

	// ======================================================================================================================================================
	// ユーティリティ [◆]
	// ======================================================================================================================================================
	// 制御点リストからnull参照を削除
	void CleanUpControlPoints()
	{
		controlPoints.RemoveAll(p => p == null);
	}

	// ======================================================================================================================================================
	// 再生 [◆]
	// ======================================================================================================================================================
	// カメラワークを開始
	// 制御点が2点以上ある場合のみ実行
	public void Play()
	{
		if (controlPoints.Count >= 2)
		{
			// タイマーをリセット
			timer = 0f;

			// 移動開始フラグを立てる
			isMoving = true;
		}
		else
		{
			// 制御点が不足している場合は警告を表示
			Debug.LogWarning("制御点は2つ以上必要です。");
		}
	}

#if UNITY_EDITOR
	// ======================================================================================================================================================
	// エディタ専用 - ギズモ描画 [◆]
	// ======================================================================================================================================================
	// シーンビューにカメラパスを可視化
	// Hermite曲線を青い線で描画
	void OnDrawGizmos()
	{
		// 制御点が不足している場合は何も描画しない
		if (controlPoints == null || controlPoints.Count < 2) return;

		// null参照を除外した有効な制御点リストを作成
		var validPoints = controlPoints.FindAll(p => p != null);
		if (validPoints.Count < 2) return;

		// ギズモの色を青に設定
		Gizmos.color = Color.cyan;

		// 曲線の分割数
		int steps = defaultSteps;

		// 各セグメントごとに曲線を描画
		for (int seg = 0; seg < validPoints.Count - 1; seg++)
		{
			Transform p0 = validPoints[0];
			Transform p1 = validPoints[1];

			// 接線ベクトルを計算
			Vector3 m1 = (p0.position - lookTargetStart.position) * easeFactor;
			Vector3 m2 = (p1.position - lookTargetEnd.position)   * easeFactor;

			// 前の点の位置（線分描画用）
			Vector3 prevPos = lookTargetStart.position;

			// 曲線を小さな線分の連続として描画
			for (int i = 1; i <= steps; i++)
			{
				// 現在の進行度
				float t  = i / (float)steps;
				float t2 = t * t;
				float t3 = t2 * t;

				// Hermite曲線上の位置を計算
				Vector3 pos =
					(2f * t3 - 3f * t2 + 1f) * lookTargetStart.position +
					(t3 - 2f * t2 + t)        * m1 +
					(-2f * t3 + 3f * t2)      * lookTargetEnd.position +
					(t3 - t2)                 * m2;

				// 前の点から現在の点へ線を描画
				Gizmos.DrawLine(prevPos, pos);
				prevPos = pos;
			}
		}
	}
#endif
}
