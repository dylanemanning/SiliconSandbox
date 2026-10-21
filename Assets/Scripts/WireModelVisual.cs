using UnityEngine;

public class WireModelVisual : MonoBehaviour
{
    public GameObject straightModel;
    public GameObject elbowModel;
    public GameObject[] branches;
    public Material lowMaterial;
    public Material highMaterial;
    private LogicWireBlock wire;
    private int oldMask = -1, oldState = -1;
    private static readonly Vector3[] Directions = {
        Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back
    };

    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (wire == null) wire = GetComponent<LogicWireBlock>();
        if (wire == null) return;
        RefreshForConnections(wire.EndpointMask | wire.ConnectionMask, wire.SignalState);
    }

    public void RefreshForConnections(int mask)
    {
        if (wire == null) wire = GetComponent<LogicWireBlock>();
        RefreshForConnections(mask, wire != null ? wire.SignalState : 0);
    }

    private void RefreshForConnections(int mask, int state)
    {
        if (mask == oldMask && state == oldState) return;
        oldMask = mask; oldState = state;
        straightModel.SetActive(false); elbowModel.SetActive(false);
        foreach (var branch in branches) branch.SetActive(false);
        Vector3 a = Vector3.zero, b = Vector3.zero;
        int count = 0;
        for (int i=0;i<6;i++) if ((mask & (1<<i)) != 0)
        {
            Vector3 local = transform.InverseTransformDirection(Directions[i]);
            if (count++ == 0) a = local; else b = local;
        }
        if (count == 0 || (count == 2 && Vector3.Dot(a,b) < -.9f))
        {
            straightModel.SetActive(true);
            straightModel.transform.localRotation = count == 0 ? Quaternion.identity
                : Quaternion.FromToRotation(Vector3.forward,a);
        }
        else if (count == 2)
        {
            elbowModel.SetActive(true);
            // Authored elbow runs from local -Z to +X.
            elbowModel.transform.localRotation = Quaternion.LookRotation(-a,Vector3.Cross(-a,b));
        }
        else
        {
            for (int i=0;i<6;i++) if ((mask & (1<<i)) != 0)
            {
                var branch = branches[i]; branch.SetActive(true);
                Vector3 local = transform.InverseTransformDirection(Directions[i]);
                branch.transform.localPosition = local*.25f;
                branch.transform.localRotation = Quaternion.FromToRotation(Vector3.forward,local);
            }
        }
        var material = state == 0 ? lowMaterial : highMaterial;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
    }
}
