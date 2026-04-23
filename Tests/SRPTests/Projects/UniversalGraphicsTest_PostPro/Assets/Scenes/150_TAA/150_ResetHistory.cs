using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ResetAttachedCameraHistory : MonoBehaviour
{
    void Start()
    {
        if (TryGetComponent<UniversalAdditionalCameraData>(out var data))
        {
            // Reset Temporal-Antialiasing and other postprocess history for consistent test results.
            data.resetHistory = true;
        }
    }
}
