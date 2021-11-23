
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Enums;

public class UdonSyncPlayerUSharp : UdonSharpBehaviour
{
    public BaseVRCVideoPlayer player;

    [UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(Url))]
    private VRCUrl url;
    public VRCUrl Url
    {
        get => url;
        set
        {
            url = value;
            if (null != player)
            {
                player.PlayURL(url);
            }
        }
    }
    
    public VRCUrlInputField urlInputField;
    
    [UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(TimeAndOffset))]
    private Vector2 timeAndOffset;
    public Vector2 TimeAndOffset
    {
        get => timeAndOffset;
        set
        {
            timeAndOffset = value;
            if (!Networking.IsOwner(this.gameObject))
            {
                SendCustomEvent(nameof(Resync));
            }
        }
    }
    
    public float syncFrequency = 15f;
    public bool allowGuestControl = true;

    public VRCUrl[] playLists;

    [UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(PlayListIndex))]
    private int playListIndex = 0;
    public int PlayListIndex
    {
        get => playListIndex;
        set
        {
            if(0 <= value && value < playLists.Length)
            {
                playListIndex = value;
            }
        }
    }

    public int startDelaySecond = 5;
    public int nextIntervalSecond = 0;
    public bool autoStart = true;
    public bool autoNext = true;
    public bool autoLoop = true;

    void Start()
    {
        if (Networking.IsOwner(this.gameObject))
        {
            if (0 < playLists.Length && autoStart)
            {
                SendCustomEventDelayedSeconds(nameof(SetUrl), startDelaySecond, EventTiming.Update);
            }
        }
    }

    public void SetUrl()
    {
        if (Networking.IsOwner(this.gameObject))
        {
            if(0 < playLists.Length)
            {
                SetProgramVariable(nameof(url), playLists[playListIndex]);

                // 同期をリクエストする
                RequestSerialization();
            }
        }
    }

    public void OnURLChanged()
    {
        var localPlayer = Networking.LocalPlayer;
        
        // Udon Graph の Is Valid ノードは null チェックと（ IValidChecker であれば ） IsValid() を行う
        // https://docs.vrchat.com/docs/players

        //if (null == localPlayer)
        if (null == localPlayer || !localPlayer.IsValid())
        {
            return;
        }
        
        Networking.SetOwner(localPlayer, this.gameObject);

        if (null == urlInputField)
        {
            return;
        }

        var inputUrl = urlInputField.GetUrl();
        SetProgramVariable(nameof(url), inputUrl);
        
        // 同期をリクエストする
        RequestSerialization();
    }

    public override void OnVideoStart()
    {
        //base.OnVideoStart();
        
        SendCustomEvent(nameof(UpdateTimeAndOffset));
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
    {
        return allowGuestControl;
        //return base.OnOwnershipRequest(requestingPlayer, requestedOwner);
    }

    public override void OnVideoEnd()
    {
        //base.OnVideoEnd();

        if (Networking.IsOwner(this.gameObject))
        {
            if (0 < playLists.Length && autoNext)
            {
                if(playListIndex < playLists.Length - 1)
                {
                    playListIndex++;
                }
                else if(autoLoop)
                {
                    playListIndex = 0;
                }

                SendCustomEventDelayedSeconds(nameof(SetUrl), nextIntervalSecond, EventTiming.Update);
            }
        }
    }

    public void UpdateTimeAndOffset()
    {
        if (!Networking.IsOwner(this.gameObject))
        {
            SendCustomEvent(nameof(Resync));
            return;
        }

        if (null == player)
        {
            return;
        }

        var serverTimeInSeconds = Networking.GetServerTimeInSeconds();
        var single = Convert.ToSingle(serverTimeInSeconds);
        var v2 = new Vector2(player.GetTime(), single);
        
        // SetProgramVariable(nameof([timeAndOffset), v2); との違いは？
        // timeAndOffset = v2; // メンバ変数への代入
        TimeAndOffset = v2; // UdonSyncedプロパティへのSet
        
        // 同期をリクエストする
        RequestSerialization();
        
        if (0 < syncFrequency)
        {
            SendCustomEventDelayedSeconds(nameof(UpdateTimeAndOffset), syncFrequency, EventTiming.Update);
        }
    }

    public void Resync()
    {
        if (null == player)
        {
            return;
        }
        
        var serverTimeInSeconds = Networking.GetServerTimeInSeconds();
        var single = Convert.ToSingle(serverTimeInSeconds);
        var x = timeAndOffset.x;
        var y = timeAndOffset.y;
        var time = x + (single - y);
        player.SetTime(time);
    }
}
