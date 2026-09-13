using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Events;

public class NetworkStateShare : MonoBehaviour, IOnEventCallback
{
    public UnityEvent onOpenDoorEvent;
    public string openDoorEvent = "open-door";
    public int mapEventId = 128;

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OpenDoor()
    {
        if (MultiplayerManager.Instance.InRoom)
        {
            PhotonView photonView = PhotonView.Get(MultiplayerManager.Instance.localPlayer);
            object[] content = new object[] { openDoorEvent };
            PhotonNetwork.RaiseEvent((byte)mapEventId, content, new RaiseEventOptions
            {
                Receivers = ReceiverGroup.Others
            }, SendOptions.SendReliable);
        }
    }

    void IOnEventCallback.OnEvent(EventData photonEvent)
    {
        HandleEvent(photonEvent);
    }

    void HandleEvent(EventData photonEvent)
    {
        if (photonEvent.Code == mapEventId && PhotonNetwork.InRoom && photonEvent.Sender > 0)
        {
            object[] array = (object[])photonEvent.CustomData;
            string e = (string)array[0];

            if (e == openDoorEvent) onOpenDoorEvent.Invoke();
        }
    }
}