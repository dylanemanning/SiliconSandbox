// PackageBlock.cs — a placed package.
//
// Like GateBlock, it is display only: the simulation driver expands the package's
// captured circuit into the netlist and pushes each port's value back in with
// SetPortState, which colours that pin. Each placement is expanded separately, so
// copies never share signals.

using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PackageBlock : LogicSignalBlock
{
    [SerializeField] private string packageId;

    private static Material bodyMaterial, topMaterial, ringMaterial, pinMaterial;
    private static readonly Color PinLow = new Color(.5f, .5f, .5f);
    private static readonly Color PinHigh = new Color(.35f, .85f, .45f);

    private readonly List<Renderer> pins = new List<Renderer>();
    private int[] portStates = new int[0];
    private bool built;
    private MaterialPropertyBlock properties;

    public string PackageId => packageId;
    public PackageDefinition Definition => PackageLibrary.Get(packageId);
    public int PortState(int index) => index >= 0 && index < portStates.Length ? portStates[index] : 0;

    public void Initialize(string id)
    {
        packageId = id;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!built) BuildVisuals();
    }

    /// <summary>Each port's index, grid cell, outward face and direction for this placement.</summary>
    public IEnumerable<(int index, PackagePort port, Vector3Int cell, Vector3Int face)> WorldPorts()
    {
        var definition = Definition;
        if (definition == null) yield break;
        for (int i = 0; i < definition.ports.Count; i++)
        {
            var (cell, face) = definition.WorldPort(i, GridPosition, transform.rotation);
            yield return (i, definition.ports[i], cell, face);
        }
    }

    public override bool HasConnection(Vector3Int cell, Vector3Int face)
    {
        foreach (var port in WorldPorts())
            if (port.cell == cell && port.face == face) return true;
        return false;
    }

    public override bool CanDrive(Vector3Int cell, Vector3Int face)
    {
        foreach (var port in WorldPorts())
            if (!port.port.isInput && port.cell == cell && port.face == face) return true;
        return false;
    }

    public override int SignalState
    {
        get
        {
            var definition = Definition;
            if (definition == null) return 0;
            for (int i = 0; i < definition.ports.Count; i++)
                if (!definition.ports[i].isInput && PortState(i) != 0) return 1;
            return 0;
        }
    }

    /// <summary>Shows a port's current value on its pin. Called by the simulation driver.</summary>
    public void SetPortState(int index, int state)
    {
        if (index < 0) return;
        if (index >= portStates.Length) System.Array.Resize(ref portStates, index + 1);
        int next = state == 0 ? 0 : 1;
        if (portStates[index] == next) return;
        portStates[index] = next;
        if (index < pins.Count && pins[index] != null)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            Color color = next == 1 ? PinHigh : PinLow;
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            pins[index].SetPropertyBlock(properties);
        }
    }

    private void BuildVisuals()
    {
        var definition = Definition;
        if (definition == null) return;
        built = true;
        EnsureMaterials();
        int width = definition.Width;
        float centerX = (width - 1) * .5f;

        // Grey body with a black top panel and inset ring, like the gate models.
        Part("Package body", PrimitiveType.Cube, new Vector3(centerX, -.03f, 0), new Vector3(width - .04f, .94f, .96f), bodyMaterial);
        Part("Package top", PrimitiveType.Cube, new Vector3(centerX, .45f, 0), new Vector3(width - .12f, .02f, .88f), topMaterial);
        float ringWidth = width - .26f, ringDepth = .74f;
        Part("Package ring front", PrimitiveType.Cube, new Vector3(centerX, .465f, ringDepth * .5f), new Vector3(ringWidth, .01f, .025f), ringMaterial);
        Part("Package ring back", PrimitiveType.Cube, new Vector3(centerX, .465f, -ringDepth * .5f), new Vector3(ringWidth, .01f, .025f), ringMaterial);
        Part("Package ring left", PrimitiveType.Cube, new Vector3(centerX - ringWidth * .5f, .465f, 0), new Vector3(.025f, .01f, ringDepth), ringMaterial);
        Part("Package ring right", PrimitiveType.Cube, new Vector3(centerX + ringWidth * .5f, .465f, 0), new Vector3(.025f, .01f, ringDepth), ringMaterial);

        // The marking, readable from behind the block (the side the player places it from).
        var label = new GameObject("Package marking", typeof(TextMeshPro));
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(centerX, .475f, 0);
        label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var text = label.GetComponent<TextMeshPro>();
        text.text = string.IsNullOrEmpty(definition.marking) ? definition.name : definition.marking;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = .5f;
        text.fontSizeMax = 3.5f;
        text.color = new Color(.92f, .95f, .92f);
        text.rectTransform.sizeDelta = new Vector2(ringWidth - .1f, ringDepth - .12f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // Pins, matching the models' diameter 0.33 and short protrusion.
        pins.Clear();
        for (int i = 0; i < definition.ports.Count; i++)
        {
            var (offset, face) = definition.LocalPort(i);
            var pin = Part("Pin " + definition.ports[i].name, PrimitiveType.Cylinder,
                (Vector3)offset + (Vector3)face * .5f, new Vector3(.33f, .0625f, .33f), pinMaterial);
            pin.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face);
            pins.Add(pin.GetComponent<Renderer>());
        }
        System.Array.Resize(ref portStates, definition.ports.Count);
    }

    private GameObject Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        var collider = part.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return part;
    }

    private static void EnsureMaterials()
    {
        if (bodyMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material Make(string name, Color color, float smoothness)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }
        bodyMaterial = Make("Package body", new Color(.62f, .63f, .66f), 0);
        topMaterial = Make("Package top", new Color(.03f, .035f, .04f), 0);
        ringMaterial = Make("Package ring", new Color(.38f, .42f, .45f), 0);
        pinMaterial = Make("Package pin", PinLow, .2f);
    }
}
