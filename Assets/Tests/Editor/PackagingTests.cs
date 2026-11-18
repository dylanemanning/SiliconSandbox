// PackagingTests.cs — packaging a circuit and reusing it (FR-5).
//
// The packaged circuit is a NAND built from an AND gate and a NOT gate. Its two
// inputs reach the box edge through wires and its output is the NOT gate's pin
// on the box edge. A pass-through wire inside the box must not become a port.
// Top view of the source circuit (x across, z up), box x 0..1, z -1..2:
//
//   z=2    NOT>   pass-through wire ->
//   z=1    [AND  out]
//   z=0    [A    B  ]
//   z=-1   wire  wire

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class PackagingTests : DriverFixture
{
    private string libraryFolder;
    private readonly List<GameObject> instances = new List<GameObject>();

    [SetUp]
    public void UseTemporaryLibrary()
    {
        libraryFolder = Path.Combine(Application.temporaryCachePath, "PackagingTests_" + System.Guid.NewGuid().ToString("N"));
        PackageLibrary.DirectoryOverride = libraryFolder;
        PackageLibrary.Reload();
    }

    [TearDown]
    public void RestoreLibrary()
    {
        foreach (var instance in instances) if (instance != null) Object.DestroyImmediate(instance);
        instances.Clear();
        PackageLibrary.DirectoryOverride = null;
        PackageLibrary.Reload();
        if (Directory.Exists(libraryFolder)) Directory.Delete(libraryFolder, true);
    }

    private GateBlock FootprintGate(GateType type, int x, int y, int z, float yaw, Vector3Int footprint)
    {
        var gate = Gate(type, x, y, z, yaw);
        gate.gameObject.AddComponent<Block>().footprint = footprint;
        return gate;
    }

    private LogicWireBlock WireAlong(int x, int y, int z, params Vector3Int[] arms)
    {
        var wire = Wire(x, y, z);
        wire.SetEndpointMask(arms.Aggregate(0, (mask, arm) => mask | LogicWireBlock.GetDirectionBit(arm)));
        return wire;
    }

    private PackageDraft NandDraft()
    {
        FootprintGate(GateType.And, 0, 0, 0, 0, new Vector3Int(2, 1, 2));
        FootprintGate(GateType.Not, 0, 0, 2, 0, Vector3Int.one);
        WireAlong(0, 0, -1, Vector3Int.forward, Vector3Int.back);
        WireAlong(1, 0, -1, Vector3Int.forward, Vector3Int.back);
        WireAlong(1, 0, 2, Vector3Int.left, Vector3Int.right); // touches only the NOT gate's side: pass-through
        return PackageCompiler.Analyze(new Vector3Int(0, 0, -1), new Vector3Int(1, 0, 2), AllPlaced());
    }

    private PackageBlock PlacePackage(PackageDefinition definition, Vector3Int anchor, float yaw)
    {
        var package = PackageLibrary.CreateInstance(definition, anchor, Quaternion.Euler(0, yaw, 0));
        instances.Add(package.gameObject);
        return package;
    }

    [Test]
    public void BoxCapturesWholeComponentsAndFindsBoundaryPorts()
    {
        PackageDraft draft = NandDraft();

        Assert.That(draft.IsValid, Is.True, string.Join("\n", draft.Errors));
        Assert.That(draft.ComponentCount, Is.EqualTo(2));
        Assert.That(draft.WireCount, Is.EqualTo(3));
        Assert.That(draft.Ports.Where(p => p.isInput).Select(p => p.name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(draft.Ports.Where(p => !p.isInput).Select(p => p.name), Is.EqualTo(new[] { "Y" }),
            "The NOT gate's edge pin is the only output; the pass-through wire is not a port.");
        var output = draft.Ports.Single(p => !p.isInput);
        Assert.That(output.insideCell, Is.EqualTo(new Vector3Int(0, 0, 3)));
        Assert.That(output.outward, Is.EqualTo(Vector3Int.forward));
    }

    [Test]
    public void PartlySelectedComponentsAndEmptyBoxesAreRejected()
    {
        FootprintGate(GateType.And, 0, 0, 0, 0, new Vector3Int(2, 1, 2));
        var cut = PackageCompiler.Analyze(Vector3Int.zero, new Vector3Int(0, 0, 1), AllPlaced());
        Assert.That(cut.IsValid, Is.False);
        Assert.That(cut.Errors[0], Does.Contain("partly inside"));

        var empty = PackageCompiler.Analyze(new Vector3Int(10, 0, 10), new Vector3Int(12, 0, 12), AllPlaced());
        Assert.That(empty.IsValid, Is.False);
    }

    [Test]
    public void DefinitionIsSavedToTheLibraryAndListedInTheInventory()
    {
        var definition = NandDraft().Build("Two input NAND", "");
        Assert.That(definition.marking, Is.EqualTo("TIN"), "The marking defaults to the name's initials.");
        Assert.That(definition.Footprint, Is.EqualTo(new Vector3Int(2, 1, 1)));
        PackageLibrary.Add(definition);
        Assert.That(File.Exists(Path.Combine(libraryFolder, definition.id + ".json")), Is.True);

        PackageLibrary.Reload();
        var loaded = PackageLibrary.Get(definition.id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.ports.Count, Is.EqualTo(3));
        var item = PackageLibrary.ItemFor(definition.id);
        Assert.That(item.DisplayName, Is.EqualTo("Two input NAND"));
        Assert.That(item.Label, Is.EqualTo("TIN"));
        Assert.That(item.Prefab.GetComponent<PackageBlock>().PackageId, Is.EqualTo(definition.id));
        Assert.That(item.Prefab.footprint, Is.EqualTo(new Vector3Int(2, 1, 1)));
    }

    // FR-5 test method: place the package in a new circuit and check its full truth table.
    [Test]
    public void PlacedPackagesKeepTheTruthTableAndRunIndependently()
    {
        if (!SimulationDriver.SimulationAvailable) Assert.Ignore("Needs the native SiliconPlugin (Windows or macOS).");
        var definition = NandDraft().Build("NAND", "NAND");
        PackageLibrary.Add(definition);

        // Copy 1 faces +Z: inputs on its back (z = -1), output in front (z = 1).
        PlacePackage(definition, new Vector3Int(10, 0, 0), 0);
        var a1 = Source(10, 0, -1);
        var b1 = Source(11, 0, -1);
        var led1 = Led(10, 0, 1);
        // Copy 2 is turned to face +X.
        var package2 = PlacePackage(definition, new Vector3Int(20, 0, 0), 90);
        var a2 = Source(19, 0, 0, 0, 90);
        var b2 = Source(19, 0, -1, 0, 90);
        var led2 = Led(21, 0, 0);

        IEnumerable<LogicSignalBlock> World() => AllPlaced().Concat(instances.Select(i => i.GetComponent<PackageBlock>()));
        foreach (var (a, b) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
        {
            a1.SetState(a); b1.SetState(b);
            a2.SetState(1 - a); b2.SetState(b);
            Driver.Simulate(World());
            Assert.That(led1.CurrentState, Is.EqualTo(a == 1 && b == 1 ? 0 : 1), $"copy 1, A={a} B={b}");
            Assert.That(led2.CurrentState, Is.EqualTo(1 - a == 1 && b == 1 ? 0 : 1), $"copy 2, A={1 - a} B={b}");
        }
        Assert.That(package2.PortState(2), Is.EqualTo(led2.CurrentState), "The output pin shows its value.");
        Assert.That(Driver.LastNetlist.Warnings, Is.Empty, string.Join("\n", Driver.LastNetlist.Warnings));
    }

    // Packages can contain packages, and wires connect to package pins.
    [Test]
    public void PackagesNestInsideOtherPackages()
    {
        if (!SimulationDriver.SimulationAvailable) Assert.Ignore("Needs the native SiliconPlugin (Windows or macOS).");
        var nand = NandDraft().Build("NAND", "NAND");
        PackageLibrary.Add(nand);
        DestroyCreatedObjects();

        // NAND package with a wire leaving each pin to the edge of a new box.
        PlacePackage(nand, new Vector3Int(0, 0, 0), 0);
        WireAlong(0, 0, -1, Vector3Int.forward, Vector3Int.back);
        WireAlong(1, 0, -1, Vector3Int.forward, Vector3Int.back);
        WireAlong(0, 0, 1, Vector3Int.forward, Vector3Int.back);
        var blocks = AllPlaced().Concat(instances.Select(i => i.GetComponent<PackageBlock>())).ToList();
        var outer = PackageCompiler.Analyze(new Vector3Int(0, 0, -1), new Vector3Int(1, 0, 1), blocks);
        Assert.That(outer.IsValid, Is.True, string.Join("\n", outer.Errors));
        Assert.That(outer.Ports.Count(p => p.isInput), Is.EqualTo(2));
        Assert.That(outer.Ports.Count(p => !p.isInput), Is.EqualTo(1));
        var wrapper = outer.Build("Wrapped NAND", "WNAND");
        PackageLibrary.Add(wrapper);

        PlacePackage(wrapper, new Vector3Int(30, 0, 0), 0);
        var a = Source(30, 0, -1, 1);
        var b = Source(31, 0, -1, 1);
        var led = Led(30, 0, 1);
        Driver.Simulate(AllPlaced().Concat(instances.Skip(1).Select(i => i.GetComponent<PackageBlock>())));
        Assert.That(led.CurrentState, Is.EqualTo(0), "1 NAND 1 = 0 through two package levels");
        b.SetState(0);
        Driver.Simulate(AllPlaced().Concat(instances.Skip(1).Select(i => i.GetComponent<PackageBlock>())));
        Assert.That(led.CurrentState, Is.EqualTo(1));
    }
}
