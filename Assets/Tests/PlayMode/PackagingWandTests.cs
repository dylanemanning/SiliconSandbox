using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PackagingWandTests
{
    private GameObject controllerObject;
    private float originalTimeScale;

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        controllerObject = new GameObject("Packaging Wand Test");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (controllerObject != null) Object.Destroy(controllerObject);
        Time.timeScale = originalTimeScale;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SelectingCornersPreviewsCuboidAndNoAllowsChoosingAnotherSecondCorner()
    {
        PackagingWandController controller = controllerObject.AddComponent<PackagingWandController>();

        controller.SelectCorner(new Vector3Int(0, 0, 0));
        Assert.That(controller.HasFirstCorner, Is.True);
        Assert.That(controller.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(12));

        controller.SelectCorner(new Vector3Int(2, 1, -1));
        Assert.That(PackagingWandController.IsConfirmationOpen, Is.True);
        Assert.That(controller.HasSecondCorner, Is.True);
        Assert.That(Time.timeScale, Is.Zero);

        Bounds outlineBounds = GetOutlineBounds(controller);
        Assert.That(outlineBounds.size, Is.EqualTo(new Vector3(3f, 2f, 2f)));

        GameObject.Find("RejectButton").GetComponent<Button>().onClick.Invoke();
        Assert.That(PackagingWandController.IsConfirmationOpen, Is.False);
        Assert.That(controller.HasFirstCorner, Is.True);
        Assert.That(controller.HasSecondCorner, Is.False);
        Assert.That(controller.FirstCorner, Is.EqualTo(Vector3Int.zero));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return null;

        controller.SelectCorner(new Vector3Int(-2, 0, 1));
        GameObject.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();
        Assert.That(controller.IsAreaConfirmed, Is.True);
        Assert.That(controller.SecondCorner, Is.EqualTo(new Vector3Int(-2, 0, 1)));
        Assert.That(PackagingWandController.IsConfirmationOpen, Is.False);
        Assert.That(controller.GetComponentsInChildren<LineRenderer>(), Is.Empty);

        controller.CancelSelection();
        controller.SelectCorner(new Vector3Int(1, 2, 3));
        Assert.That(controller.HasFirstCorner, Is.True);
        Assert.That(controller.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(12));
        controller.CancelSelection();
        Assert.That(controller.HasFirstCorner, Is.False);
        Assert.That(controller.HasSecondCorner, Is.False);
        Assert.That(controller.IsAreaConfirmed, Is.False);
        Assert.That(controller.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        yield return null;
    }

    private static Bounds GetOutlineBounds(PackagingWandController controller)
    {
        LineRenderer[] edges = controller.GetComponentsInChildren<LineRenderer>();
        Bounds bounds = new Bounds(edges[0].GetPosition(0), Vector3.zero);
        foreach (LineRenderer edge in edges)
        {
            bounds.Encapsulate(edge.GetPosition(0));
            bounds.Encapsulate(edge.GetPosition(1));
        }

        return bounds;
    }
}
