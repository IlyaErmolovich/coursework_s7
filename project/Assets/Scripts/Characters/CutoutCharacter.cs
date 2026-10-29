using System;
using UnityEngine;
namespace FrostDuel {
    /// <summary>Visual puppet only. Never writes to GameSession or the authoritative hit model.</summary>
    public sealed class CutoutCharacter : MonoBehaviour {
        public Transform torso,head,scarf,upperL,lowerL,handL,upperR,lowerR,handR,thighL,thighR,bootL,bootR;
        public Rigidbody2D[] bodies;
        public HingeJoint2D[] joints;
        public bool Fallen {get;private set;}
        public const float UpperLength=.24f,LowerLength=.20f;
        Vector3[] restPositions;Quaternion[] restRotations;Transform[] restParents;
        void Awake(){Remember();var cols=GetComponentsInChildren<Collider2D>();for(int i=0;i<cols.Length;i++)for(int k=i+1;k<cols.Length;k++)Physics2D.IgnoreCollision(cols[i],cols[k]);}
        void Remember() {
            if(restPositions!=null)return;
            restPositions=new Vector3[bodies.Length];restRotations=new Quaternion[bodies.Length];restParents=new Transform[bodies.Length];
            for(int i=0;i<bodies.Length;i++){restParents[i]=bodies[i].transform.parent;restPositions[i]=bodies[i].transform.localPosition;restRotations[i]=bodies[i].transform.localRotation;}
        }
        public void Pose(float time,float aimAngle=-1,float throwPhase=0) {
            if(Fallen)return;
            head.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*1.8f)*1.4f);
            scarf.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*2.3f)*2);
            AimArm(upperL,lowerL,handL,new Vector2(-.38f,.55f),-1);
            Vector2 target=new Vector2(.38f,.55f);
            if(aimAngle>=0) {
                // The mitten overlaps the canonical release at (.65, 1.2).
                float a=aimAngle*Mathf.Deg2Rad;target=new Vector2(.65f,1.2f)-new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.06f;
            }
            if(throwPhase>0)target=Vector2.Lerp(new Vector2(.28f,1.40f),new Vector2(.70f,.96f),Mathf.SmoothStep(0,1,throwPhase));
            AimArm(upperR,lowerR,handR,target,1);
        }
        void AimArm(Transform upper,Transform lower,Transform hand,Vector2 target,int bend) {
            Vector2 start=torso.localPosition+(Vector3)upper.localPosition;
            Vector2 delta=target-start;float d=Mathf.Clamp(delta.magnitude,.04f,UpperLength+LowerLength-.002f);
            float heading=Mathf.Atan2(delta.y,delta.x);
            float elbow=Mathf.Acos(Mathf.Clamp((UpperLength*UpperLength+d*d-LowerLength*LowerLength)/(2*UpperLength*d),-1,1));
            float upperAngle=heading+bend*elbow;
            upper.localRotation=Quaternion.Euler(0,0,upperAngle*Mathf.Rad2Deg+90);
            Vector2 tip=start+new Vector2(Mathf.Cos(upperAngle),Mathf.Sin(upperAngle))*UpperLength;
            float lowerAngle=Mathf.Atan2(target.y-tip.y,target.x-tip.x)*Mathf.Rad2Deg+90;
            lower.localRotation=Quaternion.Euler(0,0,lowerAngle-upper.localEulerAngles.z);
            hand.localRotation=Quaternion.identity;
        }
        public void Fall(Vector2 impulse) {
            Remember();Fallen=true;
            foreach(var b in bodies)b.transform.SetParent(transform,true);
            foreach(var b in bodies){b.position=b.transform.position;b.rotation=b.transform.eulerAngles.z;b.simulated=true;b.interpolation=RigidbodyInterpolation2D.Interpolate;b.bodyType=RigidbodyType2D.Dynamic;b.linearVelocity=Vector2.zero;b.angularVelocity=0;}
            foreach(var j in joints)j.enabled=true;
            bodies[0].AddForce(impulse,ForceMode2D.Impulse);bodies[0].AddTorque(impulse.x*.08f,ForceMode2D.Impulse);
        }
        public void ResetPose() {
            Remember();Fallen=false;
            foreach(var j in joints)j.enabled=false;
            for(int i=0;i<bodies.Length;i++) {
                var b=bodies[i];b.simulated=false;b.interpolation=RigidbodyInterpolation2D.None;b.bodyType=RigidbodyType2D.Kinematic;b.linearVelocity=Vector2.zero;b.angularVelocity=0;
                b.transform.SetParent(restParents[i],false);b.transform.localPosition=restPositions[i];b.transform.localRotation=restRotations[i];
            }
            Physics2D.SyncTransforms();Pose(0);
        }
        public float JointError() {
            float error=0;
            foreach(var j in joints)error=Mathf.Max(error,Vector2.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor)));
            return error;
        }
    }
}
