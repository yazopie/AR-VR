using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class RegenerableItem : MonoBehaviour
{
    public ItemSpawner spawner;
    public float distanceMin = 0.7f;

    private Vector3 posDepart;
    private bool grabbed = false;
    private XRGrabInteractable grab;

    private void Awake()
    {
        // On récupère le composant dès le début (même s’il est ajouté après)
        grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        // Sécurité : on attend la prochaine frame au cas où le grab n’est pas encore prêt
        StartCoroutine(SubscribeToGrabEvents());
    }

    private System.Collections.IEnumerator SubscribeToGrabEvents()
    {
        // On attend une frame pour être sûr que tout est initialisé
        yield return null;

        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (grab != null)
        {
            Debug.Log($"XRGrabInteractable trouvé sur {gameObject.name} !", this);

            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
        else
        {
            Debug.LogError("XR Grab Interactable MANQUANT sur " + gameObject.name);
        }
    }

    void Start() => posDepart = transform.position;

    void OnGrab(SelectEnterEventArgs _) => grabbed = true;
    void OnRelease(SelectExitEventArgs _) => grabbed = false;

    void Update()
    {
        if (grabbed && Vector3.Distance(transform.position, posDepart) > distanceMin)
        {
            spawner.Respawn();
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }
}