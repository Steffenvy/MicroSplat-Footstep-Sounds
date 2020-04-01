﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Untested

public class CollisionSounds : SurfaceSoundsUser
{
    //Fields
#if UNITY_EDITOR
    [Space(30)]
    public string currentSurfaceTypeDebug;
#endif

    [Space(30)]
    public float volumeMultiplier = 0.3f;
    [Tooltip("Non-convex MeshCollider submeshes")]
    public bool findMeshColliderSubmesh = true;

    [Space(15)]
    public Sound impactSound = new Sound();
    public float impactCooldown = 0.1f;
    [Space(15)]
    public FrictionSound frictionSound = new FrictionSound();
    public float minFrictionForce = 1;
    public float maxFrictionForce = 100;

    private float force, speed;
    private float impactCooldownT;



    //Methods
    private SurfaceSounds.SurfaceType GetSurfaceType(Collision c)
    {
        if(findMeshColliderSubmesh && c.collider is MeshCollider mc && !mc.convex)
        {
            var contact = c.GetContact(0);
            var pos = contact.point;
            var norm = contact.normal; //this better be normalized!

            float searchThickness = 0.001f + Mathf.Abs(contact.separation);

            if (mc.Raycast(new Ray(pos + norm * searchThickness, -norm), out RaycastHit rh, Mathf.Infinity)) //searchThickness * 2
            {
#if UNITY_EDITOR
                float debugSize = 3;
                Debug.DrawLine(pos + norm * debugSize, pos - norm * debugSize, Color.white, 0);
#endif

                return surfaceSounds.GetSurfaceType(c.collider, pos, rh.triangleIndex);
            }
        }

        return surfaceSounds.GetCollisionSurfaceType(c);
    }



    //Datatypes
    [System.Serializable]
    public class Sound
    {
        //Fields
        public AudioSource audioSource;

        [Header("Volume")]
        public float volumeByForce = 0.1f; //public float baseVolume = 0;

        [Header("Pitch")]
        public float basePitch = 0.5f;
        public float pitchBySpeed = 0.035f;


        //Methods
        public float Volume(float force)
        {
            return volumeByForce * force; //baseVolume + 
        }

        public float Pitch(float speed)
        {
            return basePitch + pitchBySpeed * speed;
        }
    }

    [System.Serializable]
    public class FrictionSound : Sound
    {
        //Fields
        [Header("Rates")]
        public float clipChangeSmoothTime = 0.001f;
        [Tooltip("This is used in smoothing the volume and pitch")]
        public SmoothTimes smoothTimes = SmoothTimes.Default(); //make it be smoothtime instead?

        internal SurfaceSounds.SurfaceType.SoundSet.Clip ssClip;
        private float currentVolume;
        private float currentPitch;
        private float volumeVelocity;
        private float pitchVelocity;


        //Datatypes
        [System.Serializable]
        public struct SmoothTimes
        {
            public float up;
            public float down;

            public static SmoothTimes Default()
            {
                return new SmoothTimes()
                {
                    up = 0.05f,
                    down = .15f
                };
            }
        }

        
        //Methods
        public void Update(float volumeMultiplier, float force, float speed)
        {
            if (ssClip == null)
                return;

            float targetPitch = ssClip.pitchMultiplier * Pitch(speed);

            if (audioSource.clip != ssClip.clip)
            {
                //Changes the clip if silent
                if (!Audible(currentVolume))
                {
                    audioSource.clip = ssClip.clip;
                    currentPitch = targetPitch; //Immediately changes the pitch
                    volumeVelocity = pitchVelocity = 0;
                }

                //Fades the volume to change the clip
                SmoothDamp(ref currentVolume, 0, ref volumeVelocity, new SmoothTimes() { down = clipChangeSmoothTime });
                audioSource.volume = currentVolume;
            }
            else 
            {
                if (audioSource.clip == null)
                {
                    if (audioSource.isPlaying)
                        audioSource.Stop();
                }
                else
                {
                    //Smoothly fades the pitch and volume
                    float lerpedAmount = SmoothDamp(ref currentVolume, ssClip.volumeMultiplier * Volume(force), ref volumeVelocity, smoothTimes);
                    audioSource.volume = volumeMultiplier * currentVolume;

                    if (speed != 0)
                        SmoothDamp(ref currentPitch, targetPitch, ref pitchVelocity, smoothTimes); // Mathf.LerpUnclamped(currentPitch, targetPitch, lerpedAmount);
                    audioSource.pitch = currentPitch;


                    //Ensures the AudioSource is only playing if the volume is high enough
                    bool audible = Audible(currentVolume);
                    if (audible && !audioSource.isPlaying)
                        audioSource.Play();
                    if (!audible && audioSource.isPlaying)
                        audioSource.Pause(); //perhaps Stop()?
                }
            }
        }
        private static float SmoothDamp(ref float value, float target, ref float velocity, SmoothTimes rates)
        {
            float smoothTime;
            if (target > value)
                smoothTime = rates.up;
            else
                smoothTime = rates.down;

            float maxChange = Time.deltaTime * smoothTime;

            var wantedChange = target - value;
            //var clampedChange = Mathf.Clamp(wantedChange, -maxChange, maxChange);
            //value += clampedChange;

            var before = value;
            value = Mathf.SmoothDamp(value, target, ref velocity, smoothTime);
            float clampedChange = value - before;

            if (wantedChange == 0)
                return 1;
            else
                return clampedChange / wantedChange; //returns the amount it has lerped, basically what the t would be in a Mathf.Lerp(value, target, t);
        }
        private static bool Audible(float vol)
        {
            return vol > 0.00000001f;
        }
    }



    //Lifecycle
#if UNITY_EDITOR
    private void Prepare(AudioSource source, bool loop)
    {
        source.loop = loop;
        source.playOnAwake = false;
    }
    protected override void OnValidate()
    {
        base.OnValidate();

        currentSurfaceTypeDebug = "";

        Prepare(impactSound.audioSource, false);
        Prepare(frictionSound.audioSource, true);
    }
#endif

    private void FixedUpdate()
    {
        //Clears the accumulations
        force = 0;
        speed = 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactCooldownT <= 0)
        {
            impactCooldownT = impactCooldown;

            //Impact Sound
            var vol = volumeMultiplier * impactSound.Volume(collision.impulse.magnitude); //Here "force" is actually an impulse
            var pitch = impactSound.Pitch(collision.relativeVelocity.magnitude);

            var st = GetSurfaceType(collision);
#if UNITY_EDITOR
            currentSurfaceTypeDebug = st.groupName;
#endif
            st.GetSoundSet(soundSetID).PlayOneShot(impactSound.audioSource, vol, pitch);
        }
    }
    private void OnCollisionStay(Collision collision)
    {
        var force = Mathf.Max(0, Mathf.Min(maxFrictionForce, collision.impulse.magnitude / Time.deltaTime) - minFrictionForce);
        var speed = collision.relativeVelocity.magnitude;

        this.force += force;
        this.speed += force * speed; //weights speed, so that it can find a weighted average pitch for all the potential OnCollisionStays

        var st = GetSurfaceType(collision);
#if UNITY_EDITOR
        currentSurfaceTypeDebug = st.groupName;
#endif
        frictionSound.ssClip = st.GetSoundSet(soundSetID).loopSound;
    }

    private void Update()
    {
        impactCooldownT -= Time.deltaTime;

        float speed = 0;
        if (force > 0) //prevents a divide by zero
            speed = this.speed / force;

        frictionSound.Update(volumeMultiplier, force, speed);
    }
}

/*
 *         //var norm = collision.GetContact(0).normal;

        //Debug.DrawRay(collision.GetContact(0).point, collision.impulse.normalized * 3);

        //Friction Sound
        //var force = Vector3.ProjectOnPlane(collision.impulse, norm).magnitude / Time.deltaTime; //Finds tangent force
        //var impulse = collision.impulse;
        //var force = (1 - Vector3.Dot(impulse.normalized, norm)) * impulse.magnitude / Time.deltaTime; //Finds tangent force
*/