using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using uOSC;
using UnityEngine.Events;
using EVMC4U;
using VRM;
using System;
using System.IO;
using Entum;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor;

namespace OrdinaryCartoonMaker
{
    public class VmcManager
    {
        [SerializeField]
        int port = 39539;

        private bool running = false;
        private bool recording = false;

        private VmcUOscServer server;

        public void StartVmcReceiver()
        {
            if (server == null) server = new VmcUOscServer();
            server.StartListening(port);
            Start();
            running = true;
        }

        public void StopVmcReceiver()
        {
            if (recording)
            {
                RecordEnd();
            }
            running = false;
            recording = false;
            if (server != null) server.StopListening();
        }

        public void StartVmcRecording()
        {
            recording = true;
            RecordStart();
        }

        public string StopVmcRecording()
        {
            recording = false;
            return RecordEnd();
        }

        public void Update()
        {
            //Debug.Log("Update: " + (running ? "running" : "not running") + " " + ((server==null)?"null":"not null") + " " + ((Model==null)?"null":Model.name));
            if (running && server != null && Model != null)
            {
                //Debug.Log("VMCM: Update()");
                // Read messages over VMC UPD connection.
                server.ProcessMessages();

                // Move the character's bones etc.
                //Debug.Log("model update");
                ModelUpdate();

                // If recording, copy the values over to an animation clip (via EasyMotionRecorder code).
                if (recording)
                {
                    //Debug.Log("recording update");
                    RecordingUpdate();
                }
            }
        }


        // ============== Based on uOscServer component.
        // I needed to make some methods public, and allow port number to be changed.

        public class VmcUOscServer
        {
#if NETFX_CORE
        Udp udp_ = new Uwp.Udp();
        Thread thread_ = new Uwp.Thread();
#else
            uOSC.Udp udp_ = new uOSC.DotNet.Udp();
            Thread thread_ = new uOSC.DotNet.Thread();
#endif
            Parser parser_ = new Parser();

            public class DataReceiveEvent : UnityEvent<Message> { };
            public DataReceiveEvent onDataReceived { get; private set; }

            public void StartListening(int port)
            {
                onDataReceived = new DataReceiveEvent();
                udp_.StartServer(port);
                thread_.Start(UpdateMessage);
            }

            public void StopListening()
            {
                thread_.Stop();
                udp_.Stop();
                onDataReceived = null;
            }

            public void ProcessMessages()
            {
                if (onDataReceived == null) return;

                while (parser_.messageCount > 0)
                {
                    var message = parser_.Dequeue();
                    onDataReceived.Invoke(message);
                }
            }

            // Main body of thread (asynchronously reads messages from UDP, adding them to parser queue).
            void UpdateMessage()
            {
                while (udp_.messageCount > 0)
                {
                    var buf = udp_.Receive();
                    int pos = 0;
                    parser_.Parse(buf, ref pos, buf.Length);
                }
            }
        }


        // =========================== Taken from class ExternalReceiver
        // https://sabowl.sakura.ne.jp/gpsnmeajp/
        // [Header("ExternalReceiver v3.7")]
        public GameObject Model = null;
        public bool Freeze = false; //すべての同期を止める(撮影向け)
        public bool PacktLimiter = true; //パケットフレーム数が一定値を超えるとき、パケットを捨てる

        [Header("Root Synchronize Option")]
        public Transform RootPositionTransform = null; //VR向けroot位置同期オブジェクト指定
        public Transform RootRotationTransform = null; //VR向けroot回転同期オブジェクト指定
        public bool RootPositionSynchronize = false; //AJK was true; //ルート座標同期(ルームスケール移動)
        public bool RootRotationSynchronize = false; //AJK was true; //ルート回転同期
        public bool RootScaleOffsetSynchronize = false; //MRスケール適用

        [Header("Other Synchronize Option")]
        public bool BlendShapeSynchronize = true; //表情等同期
        public bool BonePositionSynchronize = true; //ボーン位置適用(回転は強制)

        [Header("Synchronize Cutoff Option")]
        public bool HandPoseSynchronizeCutoff = false; //指状態反映オフ
        public bool EyeBoneSynchronizeCutoff = false; //目ボーン反映オフ

        [Header("Lowpass Filter Option")]
        public bool BonePositionFilterEnable = false; //ボーン位置フィルタ
        public bool BoneRotationFilterEnable = false; //ボーン回転フィルタ
        public float BoneFilter = 0.7f; //ボーンフィルタ係数
        public bool BlendShapeFilterEnable = false; //BlendShapeフィルタ
        public float BlendShapeFilter = 0.7f; //BlendShapeフィルタ係数

        [Header("Other Option")]
        public bool HideInUncalibrated = false; //キャリブレーション出来ていないときは隠す
        public bool SyncCalibrationModeWithScaleOffsetSynchronize = true; //キャリブレーションモードとスケール設定を連動させる

        [Header("Status (Read only)")]
        [SerializeField]
        private string StatusMessage = ""; //状態メッセージ(Inspector表示用)
        public string OptionString = ""; //VMCから送信されるオプション文字列

        // I removed automatic VRM file loading.
        //AJK public string loadedVRMPath = "";        //読み込み済みVRMパス
        //AJK public string loadedVRMName = "";        //読み込み済みVRM名前
        //AJK public GameObject LoadedModelParent = null; //読み込んだモデルの親

        public int LastPacketframeCounterInFrame = 0; //1フレーム中に受信したパケットフレーム数
        public int DropPackets = 0; //廃棄されたパケット(not パケットフレーム)

        public Vector3 HeadPosition = Vector3.zero;

        [Header("Daisy Chain")]
        public GameObject[] NextReceivers = new GameObject[6]; //デイジーチェーン


        //---Const---

        //rootパケット長定数(拡張判別)
        const int RootPacketLengthOfScaleAndOffset = 8;

        //---Private---

        private ExternalReceiverManager externalReceiverManager = null;

        //フィルタ用データ保持変数
        private Vector3[] bonePosFilter = new Vector3[Enum.GetNames(typeof(HumanBodyBones)).Length];
        private Quaternion[] boneRotFilter = new Quaternion[Enum.GetNames(typeof(HumanBodyBones)).Length];
        private Dictionary<string, float> blendShapeFilterDictionaly = new Dictionary<string, float>();

        //通信状態保持変数
        private int Available = 0; //データ送信可能な状態か
        private float time = 0; //送信時の時刻

        //モデル切替検出用reference保持変数
        private GameObject OldModel = null;

        //ボーン情報取得
        Animator animator = null;
        //VRMのブレンドシェーププロキシ
        VRMBlendShapeProxy blendShapeProxy = null;

        //ボーンENUM情報テーブル
        Dictionary<string, HumanBodyBones> HumanBodyBonesTable = new Dictionary<string, HumanBodyBones>();

        //ボーン情報テーブル
        Dictionary<HumanBodyBones, Vector3> HumanBodyBonesPositionTable = new Dictionary<HumanBodyBones, Vector3>();
        Dictionary<HumanBodyBones, Quaternion> HumanBodyBonesRotationTable = new Dictionary<HumanBodyBones, Quaternion>();

        //ブレンドシェープ変換テーブル
        Dictionary<string, BlendShapeKey> StringToBlendShapeKeyDictionary = new Dictionary<string, BlendShapeKey>();
        Dictionary<BlendShapeKey, float> BlendShapeToValueDictionary = new Dictionary<BlendShapeKey, float>();


        //uOSCサーバー
        //AJK uOSC.uOscServer server = null;

        //エラー・無限ループ検出フラグ(trueで一切の受信を停止する)
        bool shutdown = false;

        //フレーム間パケットフレーム数測定
        int PacketCounterInFrame = 0;

        //1フレームに30パケットフレーム来たら、同一フレーム内でそれ以上は受け取らない。
        const int PACKET_LIMIT_MAX = 30;

        //読込中は読み込まない
        bool isLoading = false;

        //メッセージ処理一時変数struct(負荷対策)
        Vector3 pos;
        Quaternion rot;
        Vector3 scale;
        Vector3 offset;

        public void Start()
        {
            //nullチェック
            if (NextReceivers == null)
            {
                NextReceivers = new GameObject[0];
            }
            //NextReciverのインターフェースを取得する
            externalReceiverManager = new ExternalReceiverManager(NextReceivers);

            //サーバーを取得
            //AJK was: server = GetComponent<uOSC.uOscServer>();
            //AJK But I needed my own VMC receiver.
            if (server != null)
            {
                //サーバーを初期化
                StatusMessage = "Waiting for VMC...";
                server.onDataReceived.AddListener(OnDataReceived);
            }
            else
            {
                //デイジーチェーンスレーブモード
                StatusMessage = "Waiting for Master...";
            }
        }

        //デイジーチェーンを更新
        public void UpdateDaisyChain()
        {
            //nullチェック
            if (NextReceivers == null)
            {
                NextReceivers = new GameObject[0];
            }
            externalReceiverManager.GetIExternalReceiver(NextReceivers);
        }

        //外部から通信状態を取得するための公開関数
        public int GetAvailable()
        {
            return Available;
        }

        //外部から通信時刻を取得するための公開関数
        public float GetRemoteTime()
        {
            return time;
        }

        // Renamed from Update() and called from Update() function above.
        public void ModelUpdate()
        {
            //エラー・無限ループ時は処理をしない
            if (shutdown) { return; }

            //Freeze有効時は動きを一切止める
            if (Freeze) { return; }

            LastPacketframeCounterInFrame = PacketCounterInFrame;
            PacketCounterInFrame = 0;

            //5.6.3p1などRunInBackgroundが既定で無効な場合Unityが極めて重くなるため対処
            Application.runInBackground = true;

            //VRMモデルからBlendShapeProxyを取得(タイミングの問題)
            if (blendShapeProxy == null && Model != null)
            {
                blendShapeProxy = Model.GetComponent<VRMBlendShapeProxy>();
            }

            //ルート位置がない場合
            if (RootPositionTransform == null && Model != null)
            {
                //モデル姿勢をルート姿勢にする
                RootPositionTransform = Model.transform;
            }

            //ルート回転がない場合
            if (RootRotationTransform == null && Model != null)
            {
                //モデル姿勢をルート姿勢にする
                RootRotationTransform = Model.transform;
            }

            //モデルがない場合はエラー表示をしておく(親切心)
            if (Model == null)
            {
                StatusMessage = "Model not found.";
                return;
            }

            //モデルが更新されたときに関連情報を更新する
            if (OldModel != Model && Model != null)
            {
                animator = Model.GetComponent<Animator>();
                blendShapeProxy = Model.GetComponent<VRMBlendShapeProxy>();
                OldModel = Model;

                Debug.Log("[ExternalReceiver] New model detected");

                //v0.56 BlendShape仕様変更対応
                //Debug.Log("-- Make BlendShapeProxy BSKey Table --");

                //BSキー値辞書の初期化(SetValueで無駄なキーが適用されるのを防止する)
                BlendShapeToValueDictionary.Clear();

                //文字-BSキー辞書の初期化(キー情報の初期化)
                StringToBlendShapeKeyDictionary.Clear();

                //全Clipsを取り出す
                foreach (var c in blendShapeProxy.BlendShapeAvatar.Clips)
                {
                    string key = "";
                    bool unknown = false;
                    //プリセットかどうかを調べる
                    if (c.Preset == BlendShapePreset.Unknown)
                    {
                        //非プリセット(Unknown)であれば、Unknown用の名前変数を参照する
                        key = c.BlendShapeName;
                        unknown = true;
                    }
                    else
                    {
                        //プリセットであればENUM値をToStringした値を利用する
                        key = c.Preset.ToString();
                        unknown = false;
                    }

                    //非ケース化するために小文字変換する
                    string lowerKey = key.ToLower();
                    //Debug.Log("Add: [key]->" + key + " [lowerKey]->" + lowerKey + " [clip]->" + c.ToString() + " [bskey]->"+c.Key.ToString() + " [unknown]->"+ unknown);

                    //小文字名-BSKeyで登録する                    
                    if (StringToBlendShapeKeyDictionary.ContainsKey(lowerKey))
                    {
                        Debug.Log("Blendshape Key already loaded: " + key + " [lowerKey]->" + lowerKey + " [clip]->" + c.ToString() + " Model.name " + Model.name);
                    }
                    else
                    {
                        StringToBlendShapeKeyDictionary.Add(lowerKey, BlendShapeKey.CreateFrom(c));
                    }
                }

                //メモ: プリセット同名の独自キー、独自キーのケース違いの重複は、共に区別しないと割り切る

                /*
                Debug.Log("-- Registered List --");
                foreach (var k in StringToBlendShapeKeyDictionary)
                {
                    Debug.Log("[k.Key]" + k.Key + " -> [k.Value.Name]" + k.Value.Name);
                }
                Debug.Log("-- End BlendShapeProxy BSKey Table --");
                */

            }

            BoneSynchronizeByTable();

        }

        //データ受信イベント
        private void OnDataReceived(uOSC.Message message)
        {
            //チェーン数0としてデイジーチェーンを発生させる
            MessageDaisyChain(ref message, 0);
        }

        //デイジーチェーン処理
        public void MessageDaisyChain(ref uOSC.Message message, int callCount)
        {
            //Startされていない場合無視
            if (externalReceiverManager == null)// AJK removed Component checks: || enabled == false || gameObject.activeInHierarchy == false)
            {
                return;
            }

            //エラー・無限ループ時は処理をしない
            if (shutdown)
            {
                return;
            }

            //パケットリミッターが有効な場合、一定以上のパケットフレーム/フレーム数を観測した場合、次のフレームまでパケットを捨てる
            if (PacktLimiter && (LastPacketframeCounterInFrame > PACKET_LIMIT_MAX))
            {
                DropPackets++;
                return;
            }

            //メッセージを処理
            if (!Freeze)
            {
                //異常を検出して動作停止
                try
                {
                    ProcessMessage(ref message);
                }
                catch (Exception e)
                {
                    StatusMessage = "Error: Exception";
                    Debug.LogError(" --- Communication Error ---");
                    Debug.LogError(e.ToString());
                    shutdown = true;
                    return;
                }
            }

            //次のデイジーチェーンへ伝える
            if (!externalReceiverManager.SendNextReceivers(message, callCount))
            {
                //無限ループ対策
                StatusMessage = "Infinite loop detected!";

                //以降の処理を全部停止
                shutdown = true;
            }
        }

        //メッセージ処理本体
        private void ProcessMessage(ref uOSC.Message message)
        {
            //メッセージアドレスがない、あるいはメッセージがない不正な形式の場合は処理しない
            if (message.address == null || message.values == null)
            {
                StatusMessage = "Bad message.";
                return;
            }

            //ルート位置がない場合
            if (RootPositionTransform == null && Model != null)
            {
                //モデル姿勢をルート姿勢にする
                RootPositionTransform = Model.transform;
            }

            //ルート回転がない場合
            if (RootRotationTransform == null && Model != null)
            {
                //モデル姿勢をルート姿勢にする
                RootRotationTransform = Model.transform;
            }

            //モーションデータ送信可否
            if (message.address == "/VMC/Ext/OK"
                && (message.values[0] is int))
            {
                Available = (int)message.values[0];
                if (Available == 0)
                {
                    StatusMessage = "Waiting for [Load VRM]";
                }

                //V2.5 キャリブレーション状態(長さ3以上)
                if (message.values.Length >= 3)
                {
                    if ((message.values[1] is int) && (message.values[2] is int))
                    {
                        int calibrationState = (int)message.values[1];
                        int calibrationMode = (int)message.values[2];

                        //キャリブレーション出来ていないときは隠す
                        if (HideInUncalibrated && Model != null)
                        {
                            Model.SetActive(calibrationState == 3);
                        }
                        //スケール同期をキャリブレーションと連動させる
                        if (SyncCalibrationModeWithScaleOffsetSynchronize)
                        {
                            RootScaleOffsetSynchronize = !(calibrationMode == 0); //通常モードならオフ、MR系ならオン
                        }

                    }
                }
                return;
            }
            //データ送信時刻
            else if (message.address == "/VMC/Ext/T"
                && (message.values[0] is float))
            {
                time = (float)message.values[0];
                PacketCounterInFrame++; //フレーム中のパケットフレーム数を測定
                return;
            }
            //VRM自動読み込み
            else if (message.address == "/VMC/Ext/VRM"
                && (message.values[0] is string)
                && (message.values[1] is string)
                )
            {
                string path = (string)message.values[0];
                string title = (string)message.values[1];

                return;
            }
            //オプション文字列
            else if (message.address == "/VMC/Ext/Opt"
                && (message.values[0] is string))
            {
                OptionString = (string)message.values[0];
                return;
            }


            //モデルがないか、モデル姿勢、ルート姿勢が取得できないなら以降何もしない
            if (Model == null || Model.transform == null || RootPositionTransform == null || RootRotationTransform == null)
            {
                return;
            }

            //Root姿勢
            if (message.address == "/VMC/Ext/Root/Pos"
                && (message.values[0] is string)
                && (message.values[1] is float)
                && (message.values[2] is float)
                && (message.values[3] is float)
                && (message.values[4] is float)
                && (message.values[5] is float)
                && (message.values[6] is float)
                && (message.values[7] is float)
                )
            {
                StatusMessage = "OK";

                pos.x = (float)message.values[1];
                pos.y = (float)message.values[2];
                pos.z = (float)message.values[3];
                rot.x = (float)message.values[4];
                rot.y = (float)message.values[5];
                rot.z = (float)message.values[6];
                rot.w = (float)message.values[7];

                //位置同期
                if (RootPositionSynchronize)
                {
                    RootPositionTransform.localPosition = pos;
                }
                //回転同期
                if (RootRotationSynchronize)
                {
                    RootRotationTransform.localRotation = rot;
                }
                //スケール同期とオフセット補正(v2.1拡張プロトコルの場合のみ)
                if (RootScaleOffsetSynchronize && message.values.Length > RootPacketLengthOfScaleAndOffset
                    && (message.values[8] is float)
                    && (message.values[9] is float)
                    && (message.values[10] is float)
                    && (message.values[11] is float)
                    && (message.values[12] is float)
                    && (message.values[13] is float)
                    )
                {
                    scale.x = 1.0f / (float)message.values[8];
                    scale.y = 1.0f / (float)message.values[9];
                    scale.z = 1.0f / (float)message.values[10];
                    offset.x = (float)message.values[11];
                    offset.y = (float)message.values[12];
                    offset.z = (float)message.values[13];

                    Model.transform.localScale = scale;
                    RootPositionTransform.localPosition = Vector3.Scale(RootPositionTransform.localPosition, scale);

                    //位置同期が有効な場合のみオフセットを反映する
                    if (RootPositionSynchronize)
                    {
                        offset = Vector3.Scale(offset, scale);
                        RootPositionTransform.localPosition -= offset;
                    }
                }
                else
                {
                    Model.transform.localScale = Vector3.one;
                }
            }
            //ボーン姿勢
            else if (message.address == "/VMC/Ext/Bone/Pos"
                && (message.values[0] is string)
                && (message.values[1] is float)
                && (message.values[2] is float)
                && (message.values[3] is float)
                && (message.values[4] is float)
                && (message.values[5] is float)
                && (message.values[6] is float)
                && (message.values[7] is float)
                )
            {
                string boneName = (string)message.values[0];
                pos.x = (float)message.values[1];
                pos.y = (float)message.values[2];
                pos.z = (float)message.values[3];
                rot.x = (float)message.values[4];
                rot.y = (float)message.values[5];
                rot.z = (float)message.values[6];
                rot.w = (float)message.values[7];

                //Debug.Log("BONE=" + boneName + " POS=" + pos.ToString() + " ROT=" + rot.ToString());


                //Humanoidボーンに該当するボーンがあるか調べる
                HumanBodyBones bone;
                if (HumanBodyBonesTryParse(ref boneName, out bone))
                {
                    //あれば位置と回転をキャッシュする
                    if (HumanBodyBonesPositionTable.ContainsKey(bone))
                    {
                        HumanBodyBonesPositionTable[bone] = pos;
                    }
                    else
                    {
                        HumanBodyBonesPositionTable.Add(bone, pos);
                    }

                    if (HumanBodyBonesRotationTable.ContainsKey(bone))
                    {
                        HumanBodyBonesRotationTable[bone] = rot;
                    }
                    else
                    {
                        HumanBodyBonesRotationTable.Add(bone, rot);
                    }
                }
                //受信と更新のタイミングは切り離した
            }
            //ブレンドシェープ同期
            else if (message.address == "/VMC/Ext/Blend/Val"
                && (message.values[0] is string)
                && (message.values[1] is float)
                )
            {
                //一旦変数に格納する
                string key = (string)message.values[0];
                float value = (float)message.values[1];

                //BlendShapeフィルタが有効なら
                if (BlendShapeFilterEnable)
                {
                    //フィルタテーブルに存在するか確認する
                    if (blendShapeFilterDictionaly.ContainsKey(key))
                    {
                        //存在する場合はフィルタ更新して値として反映する
                        blendShapeFilterDictionaly[key] = (blendShapeFilterDictionaly[key] * BlendShapeFilter) + value * (1.0f - BlendShapeFilter);
                        value = blendShapeFilterDictionaly[key];
                        //if (key == "O") Debug.Log("BSFE1: key=" + key + " val=" + value);
                    }
                    else
                    {
                        //存在しない場合はフィルタに登録する。値はそのまま
                        blendShapeFilterDictionaly.Add(key, value);
                        //if (key == "O") Debug.Log("BSFE2: key=" + key + " val=" + value);
                    }
                }

                if (BlendShapeSynchronize && blendShapeProxy != null)
                {
                    //v0.56 BlendShape仕様変更対応
                    //辞書からKeyに変換し、Key値辞書に値を入れる

                    //通信で受信したキーを小文字に変換して非ケース化
                    string lowerKey = key.ToLower();

                    //キーに該当するBSKeyが存在するかチェックする
                    BlendShapeKey bskey;
                    if (StringToBlendShapeKeyDictionary.TryGetValue(lowerKey, out bskey))
                    {
                        //キーに対して値を登録する
                        BlendShapeToValueDictionary[bskey] = value;

                        //if (lowerKey == "o") Debug.Log("[lowerKey]->"+ lowerKey+" [bskey]->"+bskey.ToString()+" [value]->"+value);
                    }
                    else
                    {
                        //そんなキーは無い
                        //AJK Debug.LogError("[lowerKey]->" + lowerKey + " is not found");
                    }
                }
            }
            //ブレンドシェープ適用
            else if (message.address == "/VMC/Ext/Blend/Apply")
            {
                if (BlendShapeSynchronize && blendShapeProxy != null)
                {
                    blendShapeProxy.SetValues(BlendShapeToValueDictionary);
                }
            }
        }

#if false // AJK
    //モデル破棄
    public void DestroyModel()
        {
            //存在すれば即破壊(異常顔防止)
            if (Model != null)
            {
                Destroy(Model);
                Model = null;
            }
            if (LoadedModelParent != null)
            {
                Destroy(LoadedModelParent);
                LoadedModelParent = null;
            }
        }
#endif

        //ボーン位置をキャッシュテーブルに基づいて更新
        private void BoneSynchronizeByTable()
        {
            //キャッシュテーブルを参照
            foreach (var bone in HumanBodyBonesTable)
            {
                //キャッシュされた位置・回転を適用
                if (HumanBodyBonesPositionTable.ContainsKey(bone.Value) && HumanBodyBonesRotationTable.ContainsKey(bone.Value))
                {
                    BoneSynchronize(bone.Value, HumanBodyBonesPositionTable[bone.Value], HumanBodyBonesRotationTable[bone.Value]);
                }
            }
        }

        //ボーン位置同期
        private void BoneSynchronize(HumanBodyBones bone, Vector3 pos, Quaternion rot)
        {
            //操作可能な状態かチェック
            if (animator != null && bone != HumanBodyBones.LastBone)
            {
                //ボーンによって操作を分ける
                var t = animator.GetBoneTransform(bone);
                if (t != null)
                {
                    //指ボーン
                    if (bone == HumanBodyBones.LeftIndexDistal ||
                        bone == HumanBodyBones.LeftIndexIntermediate ||
                        bone == HumanBodyBones.LeftIndexProximal ||
                        bone == HumanBodyBones.LeftLittleDistal ||
                        bone == HumanBodyBones.LeftLittleIntermediate ||
                        bone == HumanBodyBones.LeftLittleProximal ||
                        bone == HumanBodyBones.LeftMiddleDistal ||
                        bone == HumanBodyBones.LeftMiddleIntermediate ||
                        bone == HumanBodyBones.LeftMiddleProximal ||
                        bone == HumanBodyBones.LeftRingDistal ||
                        bone == HumanBodyBones.LeftRingIntermediate ||
                        bone == HumanBodyBones.LeftRingProximal ||
                        bone == HumanBodyBones.LeftThumbDistal ||
                        bone == HumanBodyBones.LeftThumbIntermediate ||
                        bone == HumanBodyBones.LeftThumbProximal ||

                        bone == HumanBodyBones.RightIndexDistal ||
                        bone == HumanBodyBones.RightIndexIntermediate ||
                        bone == HumanBodyBones.RightIndexProximal ||
                        bone == HumanBodyBones.RightLittleDistal ||
                        bone == HumanBodyBones.RightLittleIntermediate ||
                        bone == HumanBodyBones.RightLittleProximal ||
                        bone == HumanBodyBones.RightMiddleDistal ||
                        bone == HumanBodyBones.RightMiddleIntermediate ||
                        bone == HumanBodyBones.RightMiddleProximal ||
                        bone == HumanBodyBones.RightRingDistal ||
                        bone == HumanBodyBones.RightRingIntermediate ||
                        bone == HumanBodyBones.RightRingProximal ||
                        bone == HumanBodyBones.RightThumbDistal ||
                        bone == HumanBodyBones.RightThumbIntermediate ||
                        bone == HumanBodyBones.RightThumbProximal)
                    {
                        //指ボーンカットオフが有効でなければ
                        if (!HandPoseSynchronizeCutoff)
                        {
                            //ボーン同期する。ただしフィルタはかけない
                            BoneSynchronizeSingle(t, ref bone, ref pos, ref rot, false, false);
                        }
                    }
                    //目ボーン
                    else if (bone == HumanBodyBones.LeftEye ||
                        bone == HumanBodyBones.RightEye)
                    {
                        //目ボーンカットオフが有効でなければ
                        if (!EyeBoneSynchronizeCutoff)
                        {
                            //ボーン同期する。ただしフィルタはかけない
                            BoneSynchronizeSingle(t, ref bone, ref pos, ref rot, false, false);
                        }
                    }
                    else
                    {
                        //ボーン同期する。フィルタは設定依存
                        BoneSynchronizeSingle(t, ref bone, ref pos, ref rot, BonePositionFilterEnable, BoneRotationFilterEnable);
                    }
                }
            }
        }

        //1本のボーンの同期
        private void BoneSynchronizeSingle(Transform t, ref HumanBodyBones bone, ref Vector3 pos, ref Quaternion rot, bool posFilter, bool rotFilter)
        {
            BoneFilter = Mathf.Clamp(BoneFilter, 0f, 1f);

            //ボーン位置同期が有効か
            if (BonePositionSynchronize)
            {
                //ボーン位置フィルタが有効か
                if (posFilter)
                {
                    bonePosFilter[(int)bone] = (bonePosFilter[(int)bone] * BoneFilter) + pos * (1.0f - BoneFilter);
                    t.localPosition = bonePosFilter[(int)bone];
                }
                else
                {
                    t.localPosition = pos;
                }
            }

            //ボーン回転フィルタが有効か
            if (rotFilter)
            {
                boneRotFilter[(int)bone] = Quaternion.Slerp(boneRotFilter[(int)bone], rot, 1.0f - BoneFilter);
                t.localRotation = boneRotFilter[(int)bone];
            }
            else
            {
                t.localRotation = rot;
            }
        }

        //ボーンENUM情報をキャッシュして高速化
        private bool HumanBodyBonesTryParse(ref string boneName, out HumanBodyBones bone)
        {
            //ボーンキャッシュテーブルに存在するなら
            if (HumanBodyBonesTable.ContainsKey(boneName))
            {
                //キャッシュテーブルから返す
                bone = HumanBodyBonesTable[boneName];
                //ただしLastBoneは発見しなかったことにする(無効値として扱う)
                if (bone == HumanBodyBones.LastBone)
                {
                    return false;
                }
                return true;
            }
            else
            {
                //キャッシュテーブルにない場合、検索する
                var res = EnumTryParse<HumanBodyBones>(boneName, out bone);
                if (!res)
                {
                    //見つからなかった場合はLastBoneとして登録する(無効値として扱う)ことにより次回から検索しない
                    bone = HumanBodyBones.LastBone;
                }
                //キャシュテーブルに登録する
#if false
                HumanBodyBonesTable.Add(boneName, bone);
#else
                //AJK: Only record upper body movements.
                if (!LowerBodyBone(bone))
                {
                    HumanBodyBonesTable.Add(boneName, bone);
                }
#endif
                return res;
            }
        }

        //AJK: Return true if a lower body bone (we don't want to update these, e.g. if character is sitting).
        private bool LowerBodyBone(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Hips: return true;
                case HumanBodyBones.LeftFoot: return true;
                case HumanBodyBones.LeftLowerLeg: return true;
                case HumanBodyBones.LeftToes: return true;
                case HumanBodyBones.LeftUpperLeg: return true;
                case HumanBodyBones.RightFoot: return true;
                case HumanBodyBones.RightLowerLeg: return true;
                case HumanBodyBones.RightToes: return true;
                case HumanBodyBones.RightUpperLeg: return true;
            }
            return false;
        }

        //互換性を持ったTryParse
        private static bool EnumTryParse<T>(string value, out T result) where T : struct
        {
#if NET_4_6
            return Enum.TryParse(value, out result);
#else
            try
            {
                result = (T)Enum.Parse(typeof(T), value, true);
                return true;
            }
            catch
            {
                result = default(T);
                return false;
            }
#endif
        }
        //}



        // ========================================= Taken from EasyMotionRecorder.
        // 
        /**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/



        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private bool _recording;
        [SerializeField]
        protected int FrameIndex;

        [SerializeField, Tooltip("普段はOBJECTROOTで問題ないです。特殊な機材の場合は変更してください")]
        private MotionDataSettings.Rootbonesystem _rootBoneSystem = MotionDataSettings.Rootbonesystem.Objectroot;
        [SerializeField, Tooltip("rootBoneSystemがOBJECTROOTの時は使われないパラメータです。")]
        private HumanBodyBones _targetRootBone = HumanBodyBones.Hips;
        [SerializeField]
        private HumanBodyBones IK_LeftFootBone = HumanBodyBones.LeftFoot;
        [SerializeField]
        private HumanBodyBones IK_RightFootBone = HumanBodyBones.RightFoot;

        protected HumanoidPoses Poses;
        protected float RecordedTime;
        protected float StartTime;

        private HumanPose _currentPose;
        private HumanPoseHandler _poseHandler;
        //AJK public Action OnRecordStart;
        //AJK public Action OnRecordEnd;

        [Tooltip("記録するFPS。0で制限しない。UpdateのFPSは超えられません。")]
        public float TargetFPS = 24.0f; // AJK was 60

#if false
    // AJK: Moved this into RecordingUpdate() method, as not sure how Window lifecycle management works.
    // Use this for initialization
    private void Awake()
        {
            if (_animator == null)
            {
                _animator = Model.GetComponent<Animator>();
                if (_animator == null)
                {
                    Debug.LogError("MotionDataRecorderにanimatorがセットされていません。MotionDataRecorderを削除します。");
                    Destroy(this);
                    return;
                }
            }

            _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
        }
#endif

        // Update is called once per frame
        private void RecordingUpdate()
        {
            //Debug.Log("RecordingUpdate(): " + ((Model == null) ? "null" : Model.name) + " " + (_recording ? "recording" : "not recording"));
            if (Model == null || !_recording) return;

            if (_animator == null)
            {
                _animator = Model.GetComponent<Animator>();
                if (_animator == null)
                {
                    Debug.LogError("MotionDataRecorderにanimatorがセットされていません。MotionDataRecorderを削除します。");
                    return;
                }
            }

            _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);

            RecordedTime = Time.time - StartTime;

            if (TargetFPS != 0.0f)
            {
                var nextTime = (1.0f * (FrameIndex + 1)) / TargetFPS;
                if (nextTime > RecordedTime)
                {
                    //Debug.Log("nextTime > RecordedTime - exit");
                    return;
                }
                if (FrameIndex % TargetFPS == 0)
                {
                    //Debug.Log("Motion_FPS=" + 1 / (RecordedTime / FrameIndex));
                }
            }
            else
            {
                if (Time.frameCount % Application.targetFrameRate == 0)
                {
                    //Debug.Log("Motion_FPS=" + 1 / Time.deltaTime);
                }
            }


            //現在のフレームのHumanoidの姿勢を取得
            _poseHandler.GetHumanPose(ref _currentPose);
            //posesに取得した姿勢を書き込む
            var serializedPose = new HumanoidPoses.SerializeHumanoidPose();

            switch (_rootBoneSystem)
            {
                case MotionDataSettings.Rootbonesystem.Objectroot:
                    serializedPose.BodyRootPosition = _animator.transform.localPosition;
                    serializedPose.BodyRootRotation = _animator.transform.localRotation;
                    break;

                case MotionDataSettings.Rootbonesystem.Hipbone:
                    serializedPose.BodyRootPosition = _animator.GetBoneTransform(_targetRootBone).position;
                    serializedPose.BodyRootRotation = _animator.GetBoneTransform(_targetRootBone).rotation;
                    Debug.LogWarning(_animator.GetBoneTransform(_targetRootBone).position);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            var bodyTQ = new TQ(_currentPose.bodyPosition, _currentPose.bodyRotation);
            var LeftFootTQ = new TQ(_animator.GetBoneTransform(IK_LeftFootBone).position, _animator.GetBoneTransform(IK_LeftFootBone).rotation);
            var RightFootTQ = new TQ(_animator.GetBoneTransform(IK_RightFootBone).position, _animator.GetBoneTransform(IK_RightFootBone).rotation);
            LeftFootTQ = AvatarUtility.GetIKGoalTQ(_animator.avatar, _animator.humanScale, AvatarIKGoal.LeftFoot, bodyTQ, LeftFootTQ);
            RightFootTQ = AvatarUtility.GetIKGoalTQ(_animator.avatar, _animator.humanScale, AvatarIKGoal.RightFoot, bodyTQ, RightFootTQ);

            serializedPose.BodyPosition = bodyTQ.t;
            serializedPose.BodyRotation = bodyTQ.q;
            serializedPose.LeftfootIK_Pos = LeftFootTQ.t;
            serializedPose.LeftfootIK_Rot = LeftFootTQ.q;
            serializedPose.RightfootIK_Pos = RightFootTQ.t;
            serializedPose.RightfootIK_Rot = RightFootTQ.q;



            serializedPose.FrameCount = FrameIndex;
            serializedPose.Muscles = new float[_currentPose.muscles.Length];
            serializedPose.Time = RecordedTime;
            for (int i = 0; i < serializedPose.Muscles.Length; i++)
            {
                serializedPose.Muscles[i] = _currentPose.muscles[i];
            }

            SetHumanBoneTransformToHumanoidPoses(_animator, ref serializedPose);

            //Debug.Log("POSES[" + FrameIndex.ToString() + "]");
            Poses.Poses.Add(serializedPose);
            FrameIndex++;
        }

        /// <summary>
        /// 録画開始
        /// </summary>
        private void RecordStart()
        {
            if (_recording)
            {
                return;
            }

            Poses = ScriptableObject.CreateInstance<HumanoidPoses>();

            //AJK Removed OnRecordStart/OnRecordEnd as overkill.

            _recording = true;
            RecordedTime = 0f;
            StartTime = Time.time;
            FrameIndex = 0;
        }

        /// <summary>
        /// 録画終了
        /// </summary>
        private string RecordEnd()
        {
            if (!_recording)
            {
                return null;
            }
            _recording = false;
            return WriteAnimationFile();
        }

        private static void SetHumanBoneTransformToHumanoidPoses(Animator animator, ref HumanoidPoses.SerializeHumanoidPose pose)
        {
            HumanBodyBones[] values = Enum.GetValues(typeof(HumanBodyBones)) as HumanBodyBones[];
            foreach (HumanBodyBones b in values)
            {
                if (b < 0 || b >= HumanBodyBones.LastBone)
                {
                    continue;
                }

                Transform t = animator.GetBoneTransform(b);
                if (t != null)
                {
                    var bone = new HumanoidPoses.SerializeHumanoidPose.HumanoidBone();
                    bone.Set(animator.transform, t);
                    pose.HumanoidBones.Add(bone);
                }
            }
        }

        protected virtual string WriteAnimationFile()
        {
#if UNITY_EDITOR
            SafeCreateDirectory("Assets/Resources");

            //var path = string.Format("Assets/Resources/RecordMotion_{0}_{1:yyyy_MM_dd_HH_mm_ss}.asset", ShortenName(_animator.name), DateTime.Now);
            //var uniqueAssetPath = AssetDatabase.GenerateUniqueAssetPath(path);
            //LastClipFile = uniqueAssetPath;

            //AJK AssetDatabase.CreateAsset(Poses, uniqueAssetPath);
            //AJK AssetDatabase.Refresh();
            var animClipPath = Poses.ExportHumanoidAnim(); // Write as humanoid animation clip, not the default EasyMotionRecorder export format.

            StartTime = Time.time;
            RecordedTime = 0f;
            FrameIndex = 0;

            //AJK Move the animation clip to the episode local directory, create a timeline, etc.
            return animClipPath;
#endif
        }

        private string ShortenName(string name)
        {
            int i = 0;
            while (i < name.Length)
            {
                if (name[i] == '-' || name[i] == '.' || name[i] == '_' || name[i] == ' ')
                {
                    return name.Substring(0, i);
                }
                i++;
            }
            return name;
        }

        /// <summary>
        /// 指定したパスにディレクトリが存在しない場合
        /// すべてのディレクトリとサブディレクトリを作成します
        /// </summary>
        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            return Directory.Exists(path) ? null : Directory.CreateDirectory(path);
        }

        public Animator CharacterAnimator
        {
            get { return _animator; }
        }

        public class TQ
        {
            public TQ(Vector3 translation, Quaternion rotation)
            {
                t = translation;
                q = rotation;
            }
            public Vector3 t;
            public Quaternion q;
            // Scale should always be 1,1,1
        }

        public class AvatarUtility
        {
            static public TQ GetIKGoalTQ(Avatar avatar, float humanScale, AvatarIKGoal avatarIKGoal, TQ animatorBodyPositionRotation, TQ skeletonTQ)
            {
                int humanId = (int)HumanIDFromAvatarIKGoal(avatarIKGoal);
                if (humanId == (int)HumanBodyBones.LastBone)
                    throw new InvalidOperationException("Invalid human id.");
                MethodInfo methodGetAxisLength = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodGetAxisLength == null)
                    throw new InvalidOperationException("Cannot find GetAxisLength method.");
                MethodInfo methodGetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodGetPostRotation == null)
                    throw new InvalidOperationException("Cannot find GetPostRotation method.");
                Quaternion postRotation = (Quaternion)methodGetPostRotation.Invoke(avatar, new object[] { humanId });
                var goalTQ = new TQ(skeletonTQ.t, skeletonTQ.q * postRotation);
                if (avatarIKGoal == AvatarIKGoal.LeftFoot || avatarIKGoal == AvatarIKGoal.RightFoot)
                {
                    // Here you could use animator.leftFeetBottomHeight or animator.rightFeetBottomHeight rather than GetAxisLenght
                    // Both are equivalent but GetAxisLength is the generic way and work for all human bone
                    float axislength = (float)methodGetAxisLength.Invoke(avatar, new object[] { humanId });
                    Vector3 footBottom = new Vector3(axislength, 0, 0);
                    goalTQ.t += (goalTQ.q * footBottom);
                }
                // IK goal are in avatar body local space
                Quaternion invRootQ = Quaternion.Inverse(animatorBodyPositionRotation.q);
                goalTQ.t = invRootQ * (goalTQ.t - animatorBodyPositionRotation.t);
                goalTQ.q = invRootQ * goalTQ.q;
                goalTQ.t /= humanScale;

                return goalTQ;
            }

            static public HumanBodyBones HumanIDFromAvatarIKGoal(AvatarIKGoal avatarIKGoal)
            {
                HumanBodyBones humanId = HumanBodyBones.LastBone;
                switch (avatarIKGoal)
                {
                    case AvatarIKGoal.LeftFoot: humanId = HumanBodyBones.LeftFoot; break;
                    case AvatarIKGoal.RightFoot: humanId = HumanBodyBones.RightFoot; break;
                    case AvatarIKGoal.LeftHand: humanId = HumanBodyBones.LeftHand; break;
                    case AvatarIKGoal.RightHand: humanId = HumanBodyBones.RightHand; break;
                }
                return humanId;
            }
        }
    }
}