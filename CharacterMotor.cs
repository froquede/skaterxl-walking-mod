using UnityEngine;

namespace walking_mod
{
    // Physics side of the walking character: ground detection, snapping to slopes and stairs going down,
    // stepping up small ledges, acceleration based movement and smooth facing. Runs in FixedUpdate.
    public class CharacterMotor
    {
        public readonly Rigidbody rb;
        public readonly CapsuleCollider capsule;

        public bool grounded { get; private set; }
        public Vector3 groundNormal { get; private set; }
        public float timeSinceGrounded { get; private set; }
        // downward speed on the step the character touched ground, 0 on every other step
        public float landingSpeed { get; private set; }
        public float lastStepUpTime { get; private set; }
        // last time the body jumped in height on the ground (step up or snapping down a step)
        public float lastHeightJumpTime { get; private set; }
        // how fast the body is turning, degrees per second
        public float yawRate { get { return yawVelocity; } }

        const float MaxSlope = 50f;
        const float ContactGap = .06f;
        const float SnapDistance = .35f;
        const float StepHeight = .35f;

        float yaw, yawVelocity;
        float lastVerticalSpeed;

        public CharacterMotor(Rigidbody rb, CapsuleCollider capsule, float startYaw)
        {
            this.rb = rb;
            this.capsule = capsule;
            yaw = startYaw;
            groundNormal = Vector3.up;
            lastStepUpTime = -10f;
            lastHeightJumpTime = -10f;

            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.drag = 0f;
            rb.angularDrag = 0f;

            // no friction or bounce on the body: movement is driven by velocity, and bounciness made landings and steps hop
            PhysicMaterial material = new PhysicMaterial("Walking mod body");
            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicMaterialCombine.Minimum;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            capsule.sharedMaterial = material;
        }

        Vector3 Feet
        {
            get { return rb.position + rb.rotation * capsule.center - Vector3.up * (capsule.height * .5f); }
        }

        public void Probe(int mask, bool allowSnap, float dt)
        {
            bool wasGrounded = grounded;
            Vector3 velocity = rb.velocity;
            landingSpeed = 0f;

            float radius = capsule.radius * .9f;
            const float lift = .1f;
            Vector3 origin = Feet + Vector3.up * (capsule.radius + lift);
            RaycastHit hit;
            bool hitWalkable = false;
            float gap = float.MaxValue;

            if (Physics.SphereCast(origin, radius, Vector3.down, out hit, lift + (capsule.radius - radius) + SnapDistance, mask, QueryTriggerInteraction.Ignore))
            {
                gap = hit.distance - lift - (capsule.radius - radius);
                hitWalkable = Vector3.Angle(hit.normal, Vector3.up) <= MaxSlope;
            }

            if (stepping)
            {
                // the lift is done once the body stands on the step itself. Ending it at the step height while still over
                // the lower tread let the snap pull the body back down, which at walking speed repeated on every step
                bool onStep = Feet.y >= stepTargetY - .01f && gap <= ContactGap && hit.point.y >= stepTargetY - .06f;
                // a jump or a launch cancels the step, and it gives up if the body never gets onto the step
                if (onStep || velocity.y > 1f || Time.time - stepStartTime > .6f) stepping = false;
            }
            if (stepping)
            {
                // mid step the probe sees the lower tread or the step edge; neither should snap or drop the body
                grounded = true;
                groundNormal = Vector3.up;
                timeSinceGrounded = 0f;
                rb.useGravity = false;
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                    rb.velocity = velocity;
                }
                lastVerticalSpeed = 0f;
                return;
            }

            grounded = hitWalkable && gap <= ContactGap && velocity.y <= 1f;

            // walking off a step or down a slope: pull the body onto the ground instead of floating off it
            if (!grounded && wasGrounded && allowSnap && hitWalkable && gap <= SnapDistance && velocity.y <= .5f)
            {
                velocity.y = Mathf.Max(-gap / dt, -10f);
                rb.velocity = velocity;
                grounded = true;
                // a real drop (stairs going down), not the tiny gaps of walking down a slope
                if (gap > .04f) lastHeightJumpTime = Time.time;
            }

            if (grounded)
            {
                groundNormal = hit.normal;
                if (!wasGrounded) landingSpeed = Mathf.Max(0f, -lastVerticalSpeed);
                timeSinceGrounded = 0f;
            }
            else
            {
                groundNormal = Vector3.up;
                timeSinceGrounded += dt;
            }

            // gravity only in the air, so standing on a slope doesn't slide the frictionless body down it
            rb.useGravity = !grounded;
            if (grounded && gap <= ContactGap && rb.velocity.y < 0f)
            {
                Vector3 v = rb.velocity;
                v.y = 0f;
                rb.velocity = v;
            }

            lastVerticalSpeed = rb.velocity.y;
        }

        // a step up in progress: the body keeps rising to the step top even once the checks that started it stop seeing the riser
        bool stepping;
        float stepTargetY, stepStartTime, stepLiftSpeed;
        Vector3 stepPlanarVelocity;

        public bool isStepping { get { return stepping; } }

        // moves the horizontal velocity toward desired with separate acceleration and braking rates
        public void Move(Vector3 desired, float acceleration, float braking, float dt)
        {
            Vector3 velocity = rb.velocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            desired.y = 0f;

            bool speedingUp = desired.sqrMagnitude > .0001f && Vector3.Dot(desired, planar) >= 0f && desired.sqrMagnitude >= planar.sqrMagnitude;
            planar = Vector3.MoveTowards(planar, desired, (speedingUp ? acceleration : braking) * dt);
            rb.velocity = new Vector3(planar.x, velocity.y, planar.z);
        }

        // lifts the body onto ledges up to StepHeight in the direction of movement
        // desired is the wanted horizontal velocity, its length the target speed
        public bool StepUp(Vector3 desired, int mask, float dt)
        {
            desired.y = 0f;
            Vector3 velocity = rb.velocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);

            if (stepping && velocity.y > 1f) stepping = false;
            if (stepping)
            {
                // rise to the step height, then hold it until the body is over the step (the probe ends the step)
                float remaining = stepTargetY - Feet.y;
                if (remaining > .002f) rb.MovePosition(rb.position + Vector3.up * Mathf.Min(remaining, stepLiftSpeed * dt));
                // brushing the riser eats the forward speed; keep the speed the step started with so a flight of stairs flows
                if (desired.sqrMagnitude > .01f && planar.sqrMagnitude < stepPlanarVelocity.sqrMagnitude) planar = stepPlanarVelocity;
                rb.velocity = new Vector3(planar.x, 0f, planar.z);
                return true;
            }

            if (!grounded || desired.sqrMagnitude < .01f) return false;
            float speed = Mathf.Max(desired.magnitude, planar.magnitude);
            Vector3 direction = desired.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * (capsule.radius * .6f);

            // look ahead by about a tenth of a second so the lift finishes before the body reaches the riser
            float reach = capsule.radius + Mathf.Clamp(speed * .1f, .15f, .6f);
            Vector3 feet = Feet;
            Vector3 lowOrigin = feet + Vector3.up * .05f;

            // center and both sides of the body, so stairs taken at an angle or near an edge still register
            RaycastHit low = default(RaycastHit);
            bool found = false;
            for (int i = -1; i <= 1; i++)
            {
                RaycastHit hit;
                if (!Physics.Raycast(lowOrigin + side * i, direction, out hit, reach, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) <= MaxSlope) continue;
                if (!found || hit.distance < low.distance)
                {
                    low = hit;
                    found = true;
                }
            }
            if (!found) return false;

            // only the space the body will occupy on the step needs to be clear, not the next riser of the flight
            Vector3 high = feet + Vector3.up * (StepHeight + .02f);
            if (Physics.Raycast(high, direction, low.distance + capsule.radius, mask, QueryTriggerInteraction.Ignore)) return false;

            RaycastHit top;
            Vector3 topOrigin = high + direction * (low.distance + .08f);
            if (!Physics.Raycast(topOrigin, Vector3.down, out top, StepHeight + .05f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(top.normal, Vector3.up) > MaxSlope) return false;

            float rise = top.point.y - feet.y;
            if (rise <= .02f || rise > StepHeight) return false;

            // rise over the time left before reaching the riser, spread over a few physics steps so the interpolated body moves smoothly
            float timeToRiser = Mathf.Max(low.distance - capsule.radius, 0f) / Mathf.Max(speed, .5f);
            stepLiftSpeed = Mathf.Max(2f, (rise + .01f) / Mathf.Max(timeToRiser, .05f));
            stepTargetY = top.point.y + .01f;
            stepStartTime = Time.time;
            stepPlanarVelocity = planar.sqrMagnitude > .01f ? planar : direction * Mathf.Min(speed, 1f);
            stepping = true;
            lastStepUpTime = Time.time;

            rb.MovePosition(rb.position + Vector3.up * Mathf.Min(rise + .01f, stepLiftSpeed * dt));
            if (velocity.y < 0f) rb.velocity = planar;
            return true;
        }

        // turns the body toward a horizontal direction, keeping it upright
        public void Face(Vector3 direction, float smoothTime, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > .0001f)
            {
                float target = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, smoothTime, 900f, dt);
            }
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        }

        public void Teleport(Vector3 position, float newYaw)
        {
            yaw = newYaw;
            yawVelocity = 0f;
            rb.position = position;
            rb.rotation = Quaternion.Euler(0f, newYaw, 0f);
            rb.transform.SetPositionAndRotation(position, rb.rotation);
            rb.velocity = Vector3.zero;
            grounded = false;
            stepping = false;
            timeSinceGrounded = 0f;
            lastVerticalSpeed = 0f;
        }
    }
}
