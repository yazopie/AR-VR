using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public GameObject prefab;

    void Start() { Respawn();
        Debug.Log("Hello");
    }

    public void Update()
    {
        //Debug.Log("Hello");
    }
    public void Respawn()
    {
        foreach (Transform child in transform) Destroy(child.gameObject); // nettoie l'ancien

        Debug.Log(transform);

        GameObject nouveau = Instantiate(prefab, transform.position, transform.rotation);
        nouveau.transform.SetParent(transform);

        RegenerableItem regen = nouveau.GetComponent<RegenerableItem>();
        if (regen == null) regen = nouveau.AddComponent<RegenerableItem>();
        Debug.Log("Regen", regen);
        regen.spawner = this;
    }
}