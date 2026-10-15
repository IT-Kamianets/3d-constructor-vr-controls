using ITKamianets.Engine.Character;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.XR;
using UnityEngine;

namespace ITKamianets.Engine.VR
{
    /// <summary>
    /// The aim of the casting hand: the ray of the player's dominant controller
    /// (<see cref="EngineInput.DominantHand"/>, or <see cref="Hand"/> when set). Gameplay reads it
    /// through <see cref="IAimSource"/>, so a spell or a tool aims from the hand in VR and from the
    /// screen centre on flat screens without knowing about XR.
    /// </summary>
    [RequireComponent(typeof(XRCharacterRig))]
    public class HandAim : MonoBehaviour, IAimSource
    {
        [Tooltip("Use the player's dominant hand setting.")]
        [SerializeField] private bool useDominantHand = true;

        [SerializeField] private HandSide hand = HandSide.Right;

        private XRCharacterRig _rig;

        /// <summary>The hand that aims now.</summary>
        public HandSide Hand
        {
            get => useDominantHand ? EngineInput.Default.DominantHand : hand;
            set
            {
                hand = value;
                useDominantHand = false;
            }
        }

        /// <summary>The other hand (e.g. for a wrist menu).</summary>
        public HandSide OffHand => Hand == HandSide.Left ? HandSide.Right : HandSide.Left;

        /// <summary>The anchor of the aiming controller, or null while it isn't tracked.</summary>
        public Transform Anchor => _rig != null && _rig.IsHandTracked(Hand) ? _rig.Hand(Hand) : null;

        private void Awake()
        {
            _rig = GetComponent<XRCharacterRig>();
        }

        public bool TryGetAimRay(out Ray ray)
        {
            if (!isActiveAndEnabled || _rig == null)
            {
                ray = default;
                return false;
            }

            return _rig.TryGetHandRay(Hand, out ray);
        }
    }
}
