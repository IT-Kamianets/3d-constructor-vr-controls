using System;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.XR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ITKamianets.Engine.VR
{
    /// <summary>
    /// Turns XR controller input into a <see cref="CharacterCommand"/>, the VR counterpart of the
    /// Character View constructor's controls: the same command, so the motor, the network and
    /// bots don't care which device made it.
    ///
    /// <list type="bullet">
    /// <item>Left stick: walk relative to where the head looks (or where the off hand points).</item>
    /// <item>The character faces where the head looks; the right stick turns (the rig does it).</item>
    /// <item>Stepping in the room walks the body after the head while the stick is idle.</item>
    /// <item>Jump, Sprint (a toggle) and Crouch (a toggle) from the Player map; crouching for real
    /// also crouches.</item>
    /// </list>
    ///
    /// Put it next to the <see cref="CharacterMotor"/> and the <see cref="XRCharacterRig"/> of the
    /// local player only. Runs before the motor steps. While enabled, the Player map listens to XR
    /// controllers only, so keyboard keys used by an XR simulator don't also walk the character.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(XRCharacterRig))]
    [DefaultExecutionOrder(-40)]
    public class VRControls : MonoBehaviour
    {
        public enum MoveDirection
        {
            Head,
            OffHand,
        }

        [Serializable]
        public class VRControlsSettings
        {
            [Tooltip("Walking forward goes where the head looks, or where the off hand points.")]
            public MoveDirection MoveRelativeTo = MoveDirection.Head;

            public bool SprintToggle = true;
            public bool CrouchToggle = true;

            [Tooltip("Crouch when the real head drops below this share of standing eye height. 0 turns it off.")]
            [Range(0f, 1f)]
            public float CrouchByHeadBelow = 0.75f;

            [Tooltip("The body doesn't follow the head for smaller room steps than this, meters.")]
            public float FollowDeadZone = 0.05f;

            [Tooltip("How quickly the body catches up with the head, seconds.")]
            public float FollowTime = 0.15f;
        }

        [SerializeField] private VRControlsSettings settings = new VRControlsSettings();

        public VRControlsSettings Settings => settings;

        /// <summary>The command sent to the motor this frame; the network sends the same one to the server.</summary>
        public CharacterCommand LastCommand { get; private set; }

        private CharacterMotor _motor;
        private XRCharacterRig _rig;
        private bool _sprintToggled;
        private bool _crouchToggled;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _rig = GetComponent<XRCharacterRig>();
        }

        private void OnEnable()
        {
            EngineInput.Default.Player.Map.bindingMask = InputBinding.MaskByGroup(EngineInputActions.Groups.XR);
        }

        private void OnDisable()
        {
            EngineInput.Default.Player.Map.bindingMask = null;
            _sprintToggled = false;
            _crouchToggled = false;
            if (_rig != null)
            {
                _rig.BodyFollowsHead = false;
                _rig.Motion = 0f;
            }
        }

        private void Update()
        {
            var input = EngineInput.Default;
            if (!_rig.IsTracking)
            {
                Stand(_motor.Yaw);
                return;
            }

            if (!input.PlayerEnabled)
            {
                // A menu is open: no stick or buttons, but the body still faces the head.
                Stand(_rig.HeadYaw);
                return;
            }

            var actions = input.Player;
            var stick = Vector2.ClampMagnitude(actions.Move.ReadValue<Vector2>(), 1f);
            var basis = Quaternion.Euler(0f, MoveYaw(), 0f);
            var move = basis * new Vector3(stick.x, 0f, stick.y);

            var crouch = Crouch(actions);
            var walking = stick.sqrMagnitude > 0.01f;
            _rig.BodyFollowsHead = !walking;
            if (!walking)
            {
                move = Follow(crouch);
            }

            _rig.Motion = walking ? stick.magnitude : 0f;

            var command = new CharacterCommand
            {
                Move = move,
                Yaw = _rig.HeadYaw,
                Sprint = Sprint(actions, walking),
                Jump = actions.Jump.WasPressedThisFrame(),
                Crouch = crouch,
            };

            LastCommand = command;
            _motor.SetCommand(command);
        }

        private void Stand(float yaw)
        {
            _sprintToggled = false;
            _rig.BodyFollowsHead = false;
            _rig.Motion = 0f;
            LastCommand = new CharacterCommand { Yaw = yaw, Crouch = _crouchToggled };
            _motor.SetCommand(LastCommand);
        }

        private float MoveYaw()
        {
            if (settings.MoveRelativeTo == MoveDirection.OffHand)
            {
                var off = EngineInput.Default.DominantHand == HandSide.Left ? HandSide.Right : HandSide.Left;
                if (_rig.TryGetHandRay(off, out var ray) && Mathf.Abs(ray.direction.y) < 0.95f)
                {
                    return Mathf.Atan2(ray.direction.x, ray.direction.z) * Mathf.Rad2Deg;
                }
            }

            return _rig.HeadYaw;
        }

        // Walk the body under the head after the player steps in the room.
        private Vector3 Follow(bool crouch)
        {
            var offset = _rig.HeadOffset;
            if (offset.magnitude < settings.FollowDeadZone)
            {
                return Vector3.zero;
            }

            var speed = crouch ? _motor.Settings.CrouchSpeed : _motor.Settings.WalkSpeed;
            return Vector3.ClampMagnitude(offset / (speed * Mathf.Max(0.01f, settings.FollowTime)), 1f);
        }

        private bool Sprint(EngineInput.PlayerActions actions, bool walking)
        {
            if (!settings.SprintToggle)
            {
                _sprintToggled = false;
                return actions.Sprint.IsPressed() && walking;
            }

            if (actions.Sprint.WasPressedThisFrame())
            {
                _sprintToggled = !_sprintToggled;
            }

            if (!walking)
            {
                _sprintToggled = false;
            }

            return _sprintToggled;
        }

        private bool Crouch(EngineInput.PlayerActions actions)
        {
            bool pressed;
            if (settings.CrouchToggle)
            {
                if (actions.Crouch.WasPressedThisFrame())
                {
                    _crouchToggled = !_crouchToggled;
                }

                pressed = _crouchToggled;
            }
            else
            {
                _crouchToggled = false;
                pressed = actions.Crouch.IsPressed();
            }

            var low = settings.CrouchByHeadBelow > 0f && _rig.HeadHeight < _motor.Settings.StandingEyeHeight * settings.CrouchByHeadBelow;
            return pressed || low;
        }
    }
}
