using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SignalSimMods.FluidMovement
{
    /// <summary>
    /// Lives on the Player object next to FPSControl. Move() replaces FPSControl.PlayerMove
    /// (only called while FPSControl.CanMove), LateUpdate() layers crouch height and sway onto
    /// the camera after MouseLook_Custom has written its rotation for the frame.
    /// </summary>
    internal class FluidMovementController : MonoBehaviour
    {
        // Stock FPSControl speeds.
        private const float WalkSpeed = 4f;
        private const float SprintSpeed = 9f;

        private const float GroundAccel = 14f;
        private const float GroundFriction = 10f;
        private const float StopSpeed = 1.5f;
        private const float AirAccel = 12f;
        private const float AirTurnRate = 2.6f;       // rad/s (~150 deg/s): a 45 deg diagonal takes ~0.3 s of a ~0.6 s jump
        private const float AirSteerAccel = 2.2f;     // x wishSpeed: ~9 m/s^2 walking, ~20 m/s^2 sprinting
        private const float AirWishCap = 0.8f;      // Quake-style air strafe cap: enables speed gain when turning.
        private const float GroundStick = 6f;       // Downward push while grounded to hug slopes/stairs.
        private const float SnapDistance = 0.35f;
        private const float CoyoteTime = 0.1f;
        private const float JumpBuffer = 0.15f;
        private const float MaxFallSpeed = 55f;

        private static readonly FieldInfo AudioSpeedField = AccessTools.Field(typeof(FPSControl), "audioSpeed");
        private static readonly MethodInfo AudioStepsMethod = AccessTools.Method(typeof(FPSControl), "AudioSteps");

        private FPSControl fps;
        private CharacterController cc;
        private MouseLook_Custom look;
        private Transform cam;

        private Vector3 velocity;
        private bool grounded;
        private bool hasJumped;
        private float timeSinceGrounded;
        private float timeSinceJumpPressed = 99f;
        private float lastAudioSpeed = -1f;
        private Vector3 lastMovePos;
        private float airTime;

        // Crouch
        private float standHeight;
        private Vector3 standCenter;
        private float currentHeight;
        private bool crouchToggled;
        private bool wantsCrouch;
        private readonly Collider[] overlap = new Collider[16];

        // Camera
        // The exact resting spot PlayerAnimations moves the camera back to (it compares with ==).
        private static readonly Vector3 camBasePos = new Vector3(0f, 0.84f, 0f);
        private bool camDirty;
        private float bobPhase;
        private float bobWeight;
        private float roll, rollVel;
        private float lookSway, lookSwayVel;
        private float lastYaw;
        private float dip, dipVel;           // landing / jump spring (metres)
        private float breathT;
        private float stepSmooth, lastBodyY;
        private bool camWasGrounded;
        private float lastAirVy;

        private void Awake()
        {
            fps = GetComponent<FPSControl>();
            cc = GetComponent<CharacterController>();
            look = GetComponent<MouseLook_Custom>();
            standHeight = cc.height;
            standCenter = cc.center;
            currentHeight = standHeight;
        }

        private void Start()
        {
            cam = look != null && look.mainCamera != null ? look.mainCamera.transform : null;
            if (look != null) lastYaw = look.mouseLook.x;
            lastMovePos = transform.position;
        }

        // ---------------------------------------------------------------- movement

        internal void Move()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // Teleported by the game (Delete-key reset, save load, etc.): drop stale momentum.
            if ((transform.position - lastMovePos).sqrMagnitude > 25f) velocity = Vector3.zero;
            SaveCoordinates();

            Vector3 input = ReadMoveInput();
            bool sprint = Input.GetKey(KeyBind.keys["sprint"]) || Input.GetKey(KeyBind.keys["sprintSec"]) || Input.GetButton("Sprint");

            UpdateCrouch(dt, force: false);
            bool crouched = currentHeight < standHeight - 0.05f;

            float wishSpeed = crouched ? FluidMovementPlugin.CrouchSpeed.Value : (sprint ? SprintSpeed : WalkSpeed);
            SetAudioSpeed(crouched ? 0.8f : (sprint ? 0.4f : 0.6f));

            Vector3 wishDir = transform.TransformDirection(input);
            wishDir.y = 0f;
            float inputMag = Mathf.Clamp01(wishDir.magnitude);
            if (inputMag > 0.001f) wishDir /= wishDir.magnitude;
            wishSpeed *= inputMag;

            // Jump input (buffered; optionally held for bunny hopping).
            timeSinceJumpPressed += dt;
            if (JumpPressed()) timeSinceJumpPressed = 0f;
            bool jumpQueued = FluidMovementPlugin.EnableJump.Value &&
                (timeSinceJumpPressed <= JumpBuffer || (FluidMovementPlugin.HoldToBunnyHop.Value && JumpHeld()));

            if (grounded) timeSinceGrounded = 0f; else timeSinceGrounded += dt;
            // One jump per landing: hasJumped only clears once we touch ground again. Coyote time lets you
            // jump just after walking off a ledge, but not after a jump.
            bool canJump = !hasJumped && timeSinceGrounded <= CoyoteTime;

            Vector3 horiz = new Vector3(velocity.x, 0f, velocity.z);
            if (jumpQueued && canJump)
            {
                // Jumping on the landing frame skips ground friction entirely -> bunny hop keeps momentum.
                velocity.y = Mathf.Sqrt(2f * FluidMovementPlugin.Gravity.Value * FluidMovementPlugin.JumpHeight.Value);
                hasJumped = true;
                grounded = false;
                timeSinceJumpPressed = 99f;
                horiz = AirMove(horiz, wishDir, wishSpeed, dt);
                dipVel -= 0.25f * FluidMovementPlugin.SwayStrength.Value;   // small push-off dip
            }
            else if (grounded)
            {
                horiz = ApplyFriction(horiz, dt);
                horiz = Accelerate(horiz, wishDir, wishSpeed, GroundAccel, float.MaxValue, dt);
                velocity.y = -GroundStick;
            }
            else
            {
                horiz = AirMove(horiz, wishDir, wishSpeed, dt);
                velocity.y = Mathf.Max(velocity.y - FluidMovementPlugin.Gravity.Value * dt, -MaxFallSpeed);
            }

            float maxSpeed = FluidMovementPlugin.MaxBunnyHopSpeed.Value;
            if (horiz.sqrMagnitude > maxSpeed * maxSpeed) horiz = horiz.normalized * maxSpeed;
            velocity.x = horiz.x;
            velocity.z = horiz.z;

            float vyBefore = velocity.y;
            CollisionFlags flags = cc.Move(velocity * dt);
            Vector3 actual = cc.velocity;   // read before SnapToGround issues a second Move
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            bool nowGrounded = cc.isGrounded;
            if (!nowGrounded && !hasJumped && vyBefore <= 0f) nowGrounded = SnapToGround();
            if (nowGrounded && !grounded)
            {
                // Landed. Ignore one-frame ground flicker on slopes/bumps for the camera dip.
                if (airTime > 0.15f) OnLanded(Mathf.Min(lastAirVy, vyBefore));
                hasJumped = false;
            }
            airTime = nowGrounded ? 0f : airTime + dt;
            if (!nowGrounded) lastAirVy = velocity.y;
            grounded = nowGrounded;

            // Keep momentum only if we really moved (hitting walls kills speed).
            if (new Vector3(actual.x, 0f, actual.z).sqrMagnitude < horiz.sqrMagnitude - 0.01f)
            {
                velocity.x = actual.x;
                velocity.z = actual.z;
            }

            if (grounded && inputMag > 0.1f && !FPSControl.moving && AudioStepsMethod != null)
                fps.StartCoroutine((IEnumerator)AudioStepsMethod.Invoke(fps, null));
            lastMovePos = transform.position;
        }

        private Vector3 ReadMoveInput()
        {
            float f = 0f, r = 0f;
            if (Input.GetKey(KeyBind.keys["forward"]) || Input.GetKey(KeyBind.keys["forwardSec"]) || Input.GetKey(KeyCode.UpArrow)) f += 1f;
            else if (Input.GetKey(KeyBind.keys["backward"]) || Input.GetKey(KeyBind.keys["backwardSec"]) || Input.GetKey(KeyCode.DownArrow)) f -= 1f;
            if (Input.GetKey(KeyBind.keys["moveLeft"]) || Input.GetKey(KeyBind.keys["moveLeftSec"]) || Input.GetKey(KeyCode.LeftArrow)) r -= 1f;
            if (Input.GetKey(KeyBind.keys["moveRight"]) || Input.GetKey(KeyBind.keys["moveRightSec"]) || Input.GetKey(KeyCode.RightArrow)) r += 1f;
            if (SettingsManager.gameSettings.enableControler)
            {
                // Same axes/sign convention as stock FPSControl, but analog.
                float jf = -Input.GetAxis("Joy X Move");
                float jr = Input.GetAxis("Joy Y Move");
                if (Mathf.Abs(jf) > 0.3f) f += jf;
                if (Mathf.Abs(jr) > 0.3f) r += jr;
            }
            return Vector3.ClampMagnitude(new Vector3(r, 0f, f), 1f);
        }

        // Two parts: direct steering (pressing a direction pushes you that way, up to your walk/sprint
        // speed along it, on top of existing momentum) plus Quake-style strafe gain for bunny hopping.
        private static Vector3 AirMove(Vector3 vel, Vector3 wishDir, float wishSpeed, float dt)
        {
            // Turn existing momentum toward the pressed direction, keeping its speed. Pure accel alone barely
            // changes heading for diagonals (most of a diagonal is "already forward"), so W+A / W+D felt ignored.
            // Skipped for near-reverse input, which brakes via the accel pass instead.
            float speed = vel.magnitude;
            if (wishSpeed > 0.01f && speed > 0.5f && Vector3.Angle(vel, wishDir) < 135f)
                vel = Vector3.RotateTowards(vel / speed, wishDir, AirTurnRate * FluidMovementPlugin.AirSteering.Value * dt, 0f) * speed;

            vel = Accelerate(vel, wishDir, wishSpeed, AirSteerAccel * FluidMovementPlugin.AirSteering.Value, float.MaxValue, dt);
            return Accelerate(vel, wishDir, wishSpeed, AirAccel * FluidMovementPlugin.AirStrafeControl.Value, AirWishCap, dt);
        }

        private static Vector3 Accelerate(Vector3 vel, Vector3 wishDir, float wishSpeed, float accel, float wishCap, float dt)
        {
            if (wishSpeed <= 0f) return vel;
            float capped = Mathf.Min(wishSpeed, wishCap);
            float add = capped - Vector3.Dot(vel, wishDir);
            if (add <= 0f) return vel;
            float gain = Mathf.Min(accel * wishSpeed * dt, add);
            return vel + wishDir * gain;
        }

        private static Vector3 ApplyFriction(Vector3 vel, float dt)
        {
            float speed = vel.magnitude;
            if (speed < 0.01f) return Vector3.zero;
            float drop = Mathf.Max(speed, StopSpeed) * GroundFriction * dt;
            return vel * (Mathf.Max(speed - drop, 0f) / speed);
        }

        // Keeps us glued to slopes/stair tops when walking (not after a jump).
        private bool SnapToGround()
        {
            RaycastHit hit;
            Vector3 origin = transform.TransformPoint(cc.center);
            float half = cc.height * 0.5f - cc.radius;
            if (Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.down, out hit, half + SnapDistance, ~0, QueryTriggerInteraction.Ignore)
                && hit.distance > half + 0.01f && Vector3.Angle(hit.normal, Vector3.up) <= cc.slopeLimit)
            {
                cc.Move(Vector3.down * (hit.distance - half));
                return true;
            }
            return false;
        }

        private void OnLanded(float impactVy)
        {
            float fall = Mathf.Max(0f, -impactVy - GroundStick);
            dipVel -= Mathf.Min(fall * 0.12f, 1.6f) * FluidMovementPlugin.SwayStrength.Value;
        }

        private bool JumpPressed()
        {
            if (MenuControl.pause) return false;
            if (Input.GetKeyDown(FluidMovementPlugin.JumpKey.Value)) return true;
            return SettingsManager.gameSettings.enableControler && SafeButtonDown("Jump");
        }

        private bool JumpHeld()
        {
            if (MenuControl.pause) return false;
            if (Input.GetKey(FluidMovementPlugin.JumpKey.Value)) return true;
            return SettingsManager.gameSettings.enableControler && SafeButton("Jump");
        }

        private static bool SafeButtonDown(string name)
        {
            try { return Input.GetButtonDown(name); } catch { return false; }
        }

        private static bool SafeButton(string name)
        {
            try { return Input.GetButton(name); } catch { return false; }
        }

        private void SetAudioSpeed(float v)
        {
            if (v == lastAudioSpeed || AudioSpeedField == null) return;
            AudioSpeedField.SetValue(fps, v);
            lastAudioSpeed = v;
        }

        // Same as FPSControl.SaveCoordinates (private), which the stock PlayerMove called every frame.
        private void SaveCoordinates()
        {
            Vector3 p = transform.localPosition;
            Global.saveStruct.playerPosX = p.x;
            Global.saveStruct.playerPosY = p.y;
            Global.saveStruct.playerPosZ = p.z;
            if (look != null) Global.saveStruct.playerRotationX = look.mouseLook.x;
            if (transform.position.y <= -50f)
            {
                transform.position = new Vector3(-39f, 16f, 144f);
                velocity = Vector3.zero;
            }
        }

        // ---------------------------------------------------------------- crouch

        private void UpdateCrouch(float dt, bool force)
        {
            bool enabled = FluidMovementPlugin.EnableCrouch.Value && !force;
            bool held = false;
            if (enabled && !MenuControl.pause)
            {
                bool key = Input.GetKey(FluidMovementPlugin.CrouchKey.Value) || Input.GetKey(FluidMovementPlugin.CrouchAltKey.Value)
                           || (SettingsManager.gameSettings.enableControler && Input.GetKey(KeyCode.JoystickButton9));
                bool down = Input.GetKeyDown(FluidMovementPlugin.CrouchKey.Value) || Input.GetKeyDown(FluidMovementPlugin.CrouchAltKey.Value)
                            || (SettingsManager.gameSettings.enableControler && Input.GetKeyDown(KeyCode.JoystickButton9));
                if (FluidMovementPlugin.CrouchIsToggle.Value)
                {
                    if (down) crouchToggled = !crouchToggled;
                    held = crouchToggled;
                }
                else held = key;
            }
            else crouchToggled = false;
            wantsCrouch = held;

            float target = wantsCrouch ? standHeight * FluidMovementPlugin.CrouchHeight.Value : standHeight;
            if (!force && target > currentHeight && !HasHeadroom(target)) target = currentHeight;

            float newHeight = force ? target : Mathf.MoveTowards(currentHeight, target, standHeight * 4f * dt);
            if (Mathf.Approximately(newHeight, currentHeight) && !force) return;
            currentHeight = newHeight;
            cc.height = currentHeight;
            // Keep feet planted: shift the centre down by half the lost height.
            cc.center = standCenter - Vector3.up * ((standHeight - currentHeight) * 0.5f);
        }

        private bool HasHeadroom(float targetHeight)
        {
            float r = cc.radius * 0.95f;
            Vector3 feet = transform.TransformPoint(standCenter - Vector3.up * (standHeight * 0.5f));
            Vector3 bottom = feet + Vector3.up * (currentHeight - r);   // top sphere of the current capsule
            Vector3 top = feet + Vector3.up * (targetHeight - r);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (overlap[i] != null && !overlap[i].transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- camera

        private bool OnFoot
        {
            get
            {
                // animationComplete: the game's stand-up/dismount transition waits until the camera sits exactly
                // at its resting spot before re-enabling interaction, so hands off until it has finished.
                return FPSControl.CanMove && !PlayerControlls.playerSit && !CarControl.CanDrive
                       && PlayerAnimations.animationComplete && cam != null && cam.parent == transform;
            }
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            if (!OnFoot)
            {
                // Leaving first-person control (sitting, resting, driving, cutscene): stand up, clear offsets once,
                // and let the game own the camera.
                if (currentHeight != standHeight) UpdateCrouch(0f, force: true);
                velocity = Vector3.zero;
                if (camDirty && cam.parent == transform)
                {
                    cam.localPosition = camBasePos;
                    if (look != null) cam.localRotation = Quaternion.AngleAxis(-look.mouseLook.y, Vector3.right);
                }
                camDirty = false;
                ResetCameraState();
                return;
            }

            float dt = Time.deltaTime;
            Vector3 posOffset = Vector3.up * -(standHeight - currentHeight);
            Quaternion rotOffset = Quaternion.identity;

            if (FluidMovementPlugin.CameraSway.Value && dt > 0f && !MenuControl.pause)
            {
                float sway = FluidMovementPlugin.SwayStrength.Value;
                float bobAmt = FluidMovementPlugin.HeadBobStrength.Value;
                // Our own velocity, not cc.velocity: the latter is clobbered by the ground-snap Move and
                // flickers to zero, which made the bob stutter.
                Vector3 localVel = transform.InverseTransformDirection(new Vector3(velocity.x, 0f, velocity.z));
                float hSpeed = new Vector2(localVel.x, localVel.z).magnitude;

                // Head bob: a slow, soft side-to-side sway (one cycle per two steps) with only a
                // slight vertical component, rather than a per-step bounce.
                float targetWeight = grounded && hSpeed > 0.5f ? Mathf.Clamp(hSpeed / SprintSpeed, 0.35f, 1f) : 0f;
                bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, dt * 2f);
                bobPhase += dt * Mathf.PI * Mathf.Clamp(hSpeed, 2f, SprintSpeed) / 1.8f;
                if (bobPhase > Mathf.PI * 2f) bobPhase -= Mathf.PI * 2f;
                float bobX = Mathf.Sin(bobPhase) * 0.012f * bobWeight * bobAmt;
                float s = Mathf.Sin(bobPhase);
                float bobY = -s * s * 0.004f * bobWeight * bobAmt;   // smooth 4 mm dip per step (was 30 mm)

                // Stair/step smoothing: absorb sudden vertical pops of the body, then ease them out.
                float bodyY = transform.position.y;
                float dy = bodyY - lastBodyY;
                lastBodyY = bodyY;
                if (grounded && camWasGrounded && Mathf.Abs(dy) / dt > 6f && Mathf.Abs(dy) < 0.6f) stepSmooth -= dy;
                camWasGrounded = grounded;
                stepSmooth = Mathf.Lerp(stepSmooth, 0f, 1f - Mathf.Exp(-12f * dt));

                // Idle breathing.
                breathT += dt;
                float breath = Mathf.Sin(breathT * 1.4f) * 0.004f * (1f - Mathf.Clamp01(bobWeight)) * sway;

                // Lean into strafing.
                float targetRoll = Mathf.Clamp(-localVel.x * 0.4f, -3.5f, 3.5f) * sway;
                roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVel, 0.12f);

                // Look sway: roll slightly against quick mouse turns, springs back.
                float yaw = look != null ? look.mouseLook.x : 0f;
                float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
                lastYaw = yaw;
                float targetLook = Mathf.Clamp(-yawRate * 0.012f, -2.5f, 2.5f) * sway;
                lookSway = Mathf.SmoothDamp(lookSway, targetLook, ref lookSwayVel, 0.15f);

                // Landing / jump spring.
                dipVel += (-120f * dip - 14f * dipVel) * dt;
                dip += dipVel * dt;
                dip = Mathf.Clamp(dip, -0.25f, 0.1f);

                posOffset += new Vector3(bobX, bobY + breath + dip + stepSmooth, 0f);
                rotOffset = Quaternion.Euler(-dip * 12f, 0f, roll + lookSway);
            }
            else
            {
                ResetCameraState();
                if (look != null) lastYaw = look.mouseLook.x;
            }

            // Always write from a fixed base so nothing accumulates, even if the game snapshots the camera.
            cam.localPosition = camBasePos + posOffset;
            if (look != null && look.CanMoveMouse && look.mainCamera != null && look.mainCamera.transform == cam)
                cam.localRotation = Quaternion.AngleAxis(-look.mouseLook.y, Vector3.right) * rotOffset;
            camDirty = true;
        }

        private void ResetCameraState()
        {
            bobWeight = 0f;
            roll = rollVel = 0f;
            lookSway = lookSwayVel = 0f;
            dip = dipVel = 0f;
            stepSmooth = 0f;
            lastBodyY = transform.position.y;
        }
    }
}
