using System.Collections.Generic;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("Optional aim pivot (chest height). Defaults to this transform if null.")]
        public Transform pivot;

        public Transform Pivot => pivot != null ? pivot : transform;

        public static readonly List<LockOnTarget> All = new List<LockOnTarget>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }
}
