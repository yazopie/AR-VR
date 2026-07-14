using UnityEngine;

public class ForcePlayerStartPosition : MonoBehaviour
{
    [Header("Position de départ souhaitée dans la scène")]
    public Transform Cameratransform;
    [SerializeField] private Vector3 startPosition = new Vector3(270.25f, 1.01f, 200f);  // Exemple : sol à Y=0, yeux à 1.5m
    [SerializeField] private bool resetRotation = true;   // Remet aussi la rotation à zéro (regarde +Z)

    void Awake()
    {
        // Force la position au tout début (même avant Start)
        Cameratransform.position = startPosition;

        if (resetRotation)
            Cameratransform.rotation = Quaternion.identity; // ou Quaternion.Euler(0, 180, 0) si tu veux regarder -Z
    }
}