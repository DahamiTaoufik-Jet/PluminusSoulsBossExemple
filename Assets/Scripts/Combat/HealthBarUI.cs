using UnityEngine;
using UnityEngine.UI;

namespace Soulsboss.Combat
{
    /// <summary>
    /// Binds a UI Slider to a Health component. Drag this onto each Slider.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Tooltip("The Health component to track.")]
        public Health target;

        [Tooltip("Tag to auto-find if target is not assigned.")]
        public string targetTag;

        Slider slider;

        void Awake()
        {
            slider = GetComponent<Slider>();
        }

        void Start()
        {
            if (target == null && !string.IsNullOrEmpty(targetTag))
            {
                var go = GameObject.FindGameObjectWithTag(targetTag);
                if (go != null) target = go.GetComponentInChildren<Health>();
            }

            if (target != null && slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = target.maxHp;
                slider.value = target.current;
                slider.interactable = false;
            }
        }

        void Update()
        {
            if (target != null && slider != null)
                slider.value = target.current;
        }
    }
}
