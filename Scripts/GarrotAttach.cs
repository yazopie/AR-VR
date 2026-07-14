using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GarrotAttach : MonoBehaviour
{
    [Tooltip("Assign the XR Socket Interactor in the inspector")]
    public XRSocketInteractor socket;

    private XRGrabInteractable grabInteractable;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && socket != null)
        {
            grabInteractable.selectExited.AddListener(OnRelease);
        }
        else
        {
            Debug.LogError("Missing components: XRGrabInteractable or XRSocketInteractor");
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (socket.CanSelect((IXRSelectInteractable)grabInteractable))
        {
            socket.StartManualInteraction((IXRSelectInteractable)grabInteractable);
        }
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
}
