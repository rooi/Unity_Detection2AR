using System;
using UnityEngine;
using Unity.Barracuda;
using System.Collections;
using System.Collections.Generic;

public interface Detector
{
    int IMAGE_SIZE { get; }
    void Start();
    IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback);

}

public class DimensionsBase
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}


public class BoundingBoxDimensions : DimensionsBase { }

class CellDimensions : DimensionsBase { }

public class WorldDimensionsBase
{
    public Vector3 position { get; set; }
    public Quaternion orientation { get; set; }
    public Vector3 localScale { get; set; }
}

public class BoundingBoxWorldDimensions : WorldDimensionsBase { }


public class BoundingBox
{
    public BoundingBox()
    {
        Used = false;
    }

    ~BoundingBox()
    {
        if (CameraCopy) GameObject.Destroy(CameraCopy);
    }

    public BoundingBoxDimensions Dimensions { get; set; }

    public string Label { get; set; }

    public float Confidence { get; set; }


    public Camera CameraCopy { get; set; }

    // whether the bounding box already is used to raycast anchors
    public bool Used { get; set; }

    public Rect Rect
    {
        get { return new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
    }

    public override string ToString()
    {
        return $"{Label}:{Confidence}, {Dimensions.X}:{Dimensions.Y} - {Dimensions.Width}:{Dimensions.Height}";
    }

    public bool Equals(BoundingBox other)
    {

        //Check whether the compared object is null.
        if (BoundingBox.ReferenceEquals(other, null)) return false;

        //Check whether the compared object references the same data.
        if (BoundingBox.ReferenceEquals(this, other)) return true;

        //Check whether the products' properties are equal.
        return Rect.Equals(other.Rect) &&
               Label.Equals(other.Label) &&
               Confidence.Equals(other.Confidence);
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public override int GetHashCode()
    {

        //Get hash code for the Name field if it is not null.
        int hashProductRect = Rect == null ? 0 : Rect.GetHashCode();

        //Get hash code for the Code field.
        int hashProductLabel = Label.GetHashCode();

        //Get hash code for the Code field.
        int hashProductConfidence = Confidence.GetHashCode();

        //Calculate the hash code for the product.
        return hashProductRect ^ hashProductLabel ^ hashProductConfidence;
    }
}

class BoundingBox3D : BoundingBox
{
    public BoundingBox3D()
    {
        Used = false;
        WorldReferenced = false;
        AttachedGameObject = null;
    }

    ~BoundingBox3D()
    {
        if (AttachedGameObject)
        {
            Debug.LogError("Destroying AttachedGameObject");
            UnityEngine.Object.Destroy(AttachedGameObject);
        }
    }

    // whether the bounding box already is placed in world coordinates
    public bool WorldReferenced { get; set; }
    public BoundingBoxWorldDimensions WorldDimensions { get; set; }

    public GameObject AttachedGameObject { get; set; }
    /*
    public bool Equals(BoundingBox3D other)
    {

        //Check whether the compared object is null.
        if (BoundingBox3D.ReferenceEquals(other, null)) return false;

        //Check whether the compared object references the same data.
        if (BoundingBox3D.ReferenceEquals(this, other)) return true;

        //Check whether the products' properties are equal.
        return Code.Equals(other.Code) && Name.Equals(other.Name);
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public override int GetHashCode()
    {

        //Get hash code for the Name field if it is not null.
        int hashProductName = Name == null ? 0 : Name.GetHashCode();

        //Get hash code for the Code field.
        int hashProductCode = Code.GetHashCode();

        //Calculate the hash code for the product.
        return hashProductName ^ hashProductCode;
    }
    */
}
