using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Player
{
    private static Player editingLabelPlayer;
    private static int labelInputConsumedFrame = -1;

    private SignalLabelBlock editingSignalLabel;
    private Keyboard editingKeyboard;

    public static bool IsTypingSignalLabel =>
        editingLabelPlayer != null && editingLabelPlayer.editingSignalLabel != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSignalLabelInput()
    {
        editingLabelPlayer = null;
        labelInputConsumedFrame = -1;
    }

    private bool TryInteractSignalLabel()
    {
        if (!KeybindSettings.WasPressedThisFrame(KeybindSettings.Interact) ||
            targetBlock == null ||
            !targetBlock.TryGetComponent<SignalLabelBlock>(out var label))
            return false;

        BeginSignalLabelTyping(label);
        return true;
    }

    private void BeginSignalLabelTyping(SignalLabelBlock label)
    {
        if (editingLabelPlayer != null && editingLabelPlayer != this)
            editingLabelPlayer.EndSignalLabelTyping(false);

        editingSignalLabel = label;
        editingLabelPlayer = this;
        editingKeyboard = Keyboard.current;
        if (editingKeyboard != null)
            editingKeyboard.onTextInput += HandleLabelTextInput;

        SetWireStatus("Editing signal label: type text; Escape to finish.");
        statusUntil = float.PositiveInfinity;
    }

    private bool ProcessSignalLabelInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return true;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            EndSignalLabelTyping(true);
            return true;
        }

        if (keyboard.backspaceKey.wasPressedThisFrame)
            RemoveLastLabelTextElement();

        return true;
    }

    private void HandleLabelTextInput(char character)
    {
        if (editingSignalLabel == null) return;
        if (character == '\r') return;
        if (character == '\n' || !char.IsControl(character))
            editingSignalLabel.SetLabelText(editingSignalLabel.LabelText + character);
    }

    private void RemoveLastLabelTextElement()
    {
        string text = editingSignalLabel.LabelText;
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        if (starts.Length == 0) return;

        editingSignalLabel.SetLabelText(text.Substring(0, starts[starts.Length - 1]));
    }

    private void EndSignalLabelTyping(bool escaped)
    {
        bool wasEditing = editingLabelPlayer == this;
        if (editingKeyboard != null)
            editingKeyboard.onTextInput -= HandleLabelTextInput;

        editingKeyboard = null;
        editingSignalLabel = null;
        if (editingLabelPlayer == this) editingLabelPlayer = null;

        if (escaped)
        {
            labelInputConsumedFrame = Time.frameCount;
            SetWireStatus("Signal label updated.");
        }
        else if (wasEditing && wireStatus != null)
        {
            wireStatus.text = string.Empty;
            statusUntil = Time.unscaledTime;
        }
    }

    private void TryPlaceSignalLabel(Block prefab)
    {
        if (!TryGetSignalLabelAttachment(out Vector3Int cell, out Vector3Int face))
            return;

        Camera camera = cameraSettings.camera;
        Vector3 normal = face;
        Vector3 attachmentPoint = (Vector3)cell + normal * 0.5f;
        Vector3 tangent = camera != null
            ? Vector3.ProjectOnPlane(camera.transform.right, normal)
            : Vector3.zero;
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.forward);
        tangent.Normalize();

        Vector3 position = attachmentPoint + normal * 0.1f + tangent * 0.5f + Vector3.up * 0.1f;
        Vector3 facing = camera != null
            ? Vector3.ProjectOnPlane(camera.transform.position - position, Vector3.up)
            : Vector3.zero;
        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.ProjectOnPlane(normal, Vector3.up);
        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

        GameObject placedObject = Instantiate(prefab.gameObject, position, rotation);
        SignalLabelBlock label = placedObject.GetComponent<SignalLabelBlock>();
        if (label == null)
        {
            Debug.LogError($"Signal label prefab '{prefab.name}' has no SignalLabelBlock component.", prefab);
            Destroy(placedObject);
            return;
        }

        label.SetAttachment(cell, face);
        WorldSaveSystem.Instance?.MarkDirty();
    }

    private bool TryGetSignalLabelAttachment(out Vector3Int cell, out Vector3Int face)
    {
        cell = default;
        face = Vector3Int.RoundToInt(SnapToCardinalAxis(targetRaycastHit.normal));
        if (targetBlock == null) return false;

        if (targetBlock.TryGetComponent<LogicWireBlock>(out var wire))
        {
            cell = wire.GridPosition;
            return true;
        }

        if (!targetBlock.TryGetComponent<GateBlock>(out var gate)) return false;

        float bestDistance = 0.35f * 0.35f;
        bool found = false;
        foreach (var port in gate.WorldPorts())
        {
            if (port.face != face) continue;

            float distance = ((Vector3)port.cell + (Vector3)port.face * 0.5f -
                targetRaycastHit.point).sqrMagnitude;
            if (distance > bestDistance) continue;

            bestDistance = distance;
            cell = port.cell;
            face = port.face;
            found = true;
        }

        return found;
    }
}
