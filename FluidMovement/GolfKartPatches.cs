using HarmonyLib;
using UnityEngine;

namespace SignalSimMods.FluidMovement
{
    /// <summary>
    /// Light-touch golf kart handling fixes (FluidMovementPlugin.ImprovedKart). Stock problems:
    ///  - steering moves 2 degrees per *frame* up to 45 degrees at any speed; the tyres scrub off speed in turns
    ///  - drive torque is a hard on/off switch at 40 m/s
    ///  - touching anything not tagged "Terrain" above 10 m/s zeroes the kart's velocity
    /// Top speeds, torque, braking and reversing are left as stock.
    /// </summary>
    internal static class GolfKartPatches
    {
        private const float MaxSpeed = 40f;          // stock forward cap
        private const float Torque = 800f;           // stock forward torque
        private const float LowSpeedSteer = 40f;
        private const float SteerRate = 110f;        // deg/s toward input
        private const float ReturnRate = 150f;       // deg/s back to centre
        private const float CrashImpact = 12f;       // m/s head-on into a wall/object still stops you dead

        private static float steer;

        private static float SpeedFB(CarControl car)
        {
            Vector3 v = CarControl.rb.velocity;
            return Vector3.Dot(car.transform.forward, v) > 0f ? v.magnitude : -v.magnitude;
        }

        [HarmonyPatch(typeof(CarControl), "DriveForward")]
        private static class DriveForwardPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(CarControl __instance)
            {
                if (!FluidMovementPlugin.ImprovedKart.Value) return true;
                float v = SpeedFB(__instance);
                var wheels = __instance.wheelColider;
                if (v < -3f)
                {
                    // Rolling backwards: brake first (stock behaviour).
                    for (int i = 0; i < wheels.Length; i++) { wheels[i].motorTorque = 0f; wheels[i].brakeTorque = 500f; }
                }
                else
                {
                    // Full torque until ~70% of top speed, then taper smoothly to zero (no on/off surging).
                    float t = Torque * Mathf.Clamp01((MaxSpeed - v) / (MaxSpeed * 0.3f));
                    for (int i = 0; i < wheels.Length; i++) { wheels[i].motorTorque = t; wheels[i].brakeTorque = 0f; }
                }
                for (int j = 0; j < __instance.breakLights.Length; j++) __instance.breakLights[j].enabled = false;
                if (v > 20f)
                {
                    var emission = __instance.smoke.emission;
                    emission.rateOverTime = 20f;
                }
                return false;
            }
        }

        // Runs after stock Update has nudged the steer angle; overrides it with a smoothed, speed-sensitive value.
        [HarmonyPatch(typeof(CarControl), "Update")]
        private static class SteeringPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CarControl __instance)
            {
                var wheels = __instance.wheelColider;
                if (!FluidMovementPlugin.ImprovedKart.Value || !CarControl.CanDrive || wheels == null || wheels.Length < 2)
                {
                    if (wheels != null && wheels.Length > 0) steer = wheels[0].steerAngle;
                    return;
                }

                float input = 0f;
                if (Input.GetKey(KeyBind.keys["moveRight"]) || Input.GetKey(KeyBind.keys["moveRightSec"]) || Input.GetKey(KeyCode.RightArrow)) input += 1f;
                if (Input.GetKey(KeyBind.keys["moveLeft"]) || Input.GetKey(KeyBind.keys["moveLeftSec"]) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
                if (input == 0f && SettingsManager.gameSettings.enableControler)
                {
                    float axis = Input.GetAxis("Joy Y Move");
                    if (Mathf.Abs(axis) > 0.15f) input = Mathf.Clamp(axis, -1f, 1f);
                }

                float speed = Mathf.Abs(SpeedFB(__instance));
                float maxSteer = Mathf.Lerp(LowSpeedSteer, FluidMovementPlugin.KartHighSpeedSteer.Value, Mathf.InverseLerp(5f, 25f, speed));
                float target = input * maxSteer;
                float rate = (input == 0f || Mathf.Sign(target) != Mathf.Sign(steer)) ? ReturnRate : SteerRate;
                steer = Mathf.MoveTowards(steer, target, rate * Time.deltaTime);

                wheels[0].steerAngle = steer;
                wheels[1].steerAngle = steer;
                Vector3 e = __instance.steeringWheel.transform.localEulerAngles;
                e.y = steer * 4f;
                __instance.steeringWheel.transform.localEulerAngles = e;
            }
        }

        [HarmonyPatch(typeof(CarControl), "OnCollisionEnter")]
        private static class CollisionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(CarControl __instance, Collision other)
            {
                if (!FluidMovementPlugin.ImprovedKart.Value) return true;
                var rb = CarControl.rb;
                if (!CarControl.CanDrive)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    return false;
                }
                if (other.collider.CompareTag("Terrain")) return false;

                // Only a genuine head-on hit into something wall-like stops the kart dead; bumps, kerbs,
                // road meshes and glancing scrapes are left to the physics.
                for (int i = 0; i < other.contactCount; i++)
                {
                    Vector3 n = other.GetContact(i).normal;
                    if (Mathf.Abs(n.y) > 0.45f) continue;
                    if (Mathf.Abs(Vector3.Dot(other.relativeVelocity, n)) > CrashImpact)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        break;
                    }
                }
                return false;
            }
        }
    }
}
