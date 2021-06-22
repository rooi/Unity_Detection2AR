using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Merge3DBoundingBoxes : MonoBehaviour
{
    public string label = "";
    public float confidence;
    public long nrOfDetections = 0;
    public double lastDetection = 0;
    public bool scheduleDestroy = false;

    // Start is called before the first frame update
    void Start()
    {
        lastDetection = Time.fixedTimeAsDouble;

        GetComponent<BoxCollider>().isTrigger = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (scheduleDestroy) GameObject.Destroy(transform.parent.gameObject);

        double currentTime = Time.fixedTimeAsDouble;
        if ((currentTime - lastDetection) > 10 && nrOfDetections < 3) scheduleDestroy = true;
    }

    //If your GameObject starts to collide with another GameObject with a Collider
    //void OnCollisionEnter(Collision collision)
    //{
    //    //Output the Collider's GameObject's name
    //    Debug.Log("OnCollisionEnter " + collision.collider.name);
    //}

    //If your GameObject keeps colliding with another GameObject with a Collider, do something
    //void OnCollisionStay(Collision collision)
    //{
    //    Debug.Log("OnCollisionStay " + collision.collider.name);
    //}

    private void OnTriggerStay(Collider other)
    {
        //Debug.Log("OnTriggerStay " + other.name);
        GameObject go1 = gameObject;
        GameObject go2 = other.gameObject;

        Merge3DBoundingBoxes mbb1 = go1.GetComponent<Merge3DBoundingBoxes>() as Merge3DBoundingBoxes;
        Merge3DBoundingBoxes mbb2 = go2.GetComponent<Merge3DBoundingBoxes>() as Merge3DBoundingBoxes;

        // Only check againt boundingboxes
        if (mbb1 && mbb2 && !mbb1.scheduleDestroy && !mbb2.scheduleDestroy)
        {
            Debug.Log("mbb1.label = " + mbb1.label + ", mbb2.label = " + mbb2.label);
            //if (mbb1.label == mbb2.label)
            {
                if (mbb1.confidence <= mbb2.confidence && (mbb1.lastDetection < mbb2.lastDetection || mbb1.nrOfDetections < mbb2.nrOfDetections))
                {
                    mbb1.scheduleDestroy = true;
                    //GameObject.Destroy(go1.transform.parent.gameObject);
                }
            }

            // Leave these at the end
            mbb1.nrOfDetections++;
            mbb1.lastDetection = Time.fixedTimeAsDouble;
        }
    }
}
