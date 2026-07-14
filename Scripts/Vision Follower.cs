using UnityEngine;

public class SimpleFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform player; // Glisse XR Origin ou laisse vide

    [Header("Position relative (visible et stable)")]
    public float devant = 0.7f;   // Devant
    public float droite = 0.65f;  // Droite
    public float hauteur = 1f;  // Hauteur yeux

    private Rigidbody rb; // ← FIX GRAVITÉ

    void Start()
    {
        if (player == null) player = Camera.main.transform;

        // FIX GRAVITÉ : Désactive la physique pour que le cube ne tombe plus
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;  // Ignore gravité + collisions physiques
            rb.useGravity = false;  // Double sécurité
        }
    }

    void LateUpdate()
    {
        transform.position = player.position
                           + player.forward * devant
                           + player.right * droite
                           + player.up * hauteur;

        // Toujours face au joueur → surface visible
        transform.rotation = Quaternion.LookRotation(transform.position - player.position);
    }
}