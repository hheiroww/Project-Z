using System.Numerics;
using ProjectZ.Shared.Drawing.ThreeD;
using Silk.NET.Assimp;

namespace ProjectZ.ModelImport;

public static unsafe partial class AssimpModelBridge
{
    private static ImportedModel ImportWithHierarchy(Scene* scene, ImportedMaterial[] materials)
    {
        if (scene->MRootNode is null)
            throw new InvalidDataException("The model has no root scene node.");

        var nodes = new List<(nint Address, Matrix4x4 World)>();
        var joints = new List<ModelJoint3D>();
        var nodeIndices = new Dictionary<nint, int>();
        var nameIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var pending = new Stack<(nint Address, int Parent)>();
        pending.Push(((nint)scene->MRootNode, -1));
        while (pending.TryPop(out var next))
        {
            var node = (Node*)next.Address;
            int index = joints.Count;
            if (node is null || !nodeIndices.TryAdd(next.Address, index))
                throw new InvalidDataException("The model scene hierarchy is invalid.");

            string name = node->MName.AsString;
            if (!nameIndices.TryAdd(name, index))
                nameIndices[name] = -1;
            // Assimp stores transforms for column vectors. The runtime uses row vectors.
            var local = Matrix4x4.Transpose(node->MTransformation);
            var world = next.Parent < 0 ? local : local * nodes[next.Parent].World;
            nodes.Add((next.Address, world));
            joints.Add(new ModelJoint3D(string.IsNullOrWhiteSpace(name) ? $"Node {index + 1}" : name, next.Parent, local));

            for (uint count = node->MNumChildren; count > 0; count--)
                pending.Push(((nint)node->MChildren[count - 1], index));
        }

        var animations = new List<ModelAnimation3D>();
        for (uint i = 0; i < scene->MNumAnimations; i++)
        {
            if (ReadAnimation(scene->MAnimations[i], i, nameIndices) is { } animation)
                animations.Add(animation);
        }

        var meshes = new List<ImportedMesh>();
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            var (address, world) = nodes[nodeIndex];
            var node = (Node*)address;
            for (uint i = 0; i < node->MNumMeshes; i++)
            {
                uint meshIndex = node->MMeshes[i];
                if (meshIndex >= scene->MNumMeshes)
                    throw new InvalidDataException("A scene node references a missing mesh.");
                var mesh = scene->MMeshes[meshIndex];
                if (mesh is null)
                    throw new InvalidDataException("A scene node references an empty mesh.");
                var binding = new ModelMeshBinding3D(nodeIndex, ReadBones(mesh, nodeIndices, nameIndices));
                if (ReadMesh(mesh, world, binding) is { } surface)
                    meshes.Add(surface);
            }
        }

        if (meshes.Count == 0)
            throw new InvalidDataException("The model contains no triangle surfaces.");
        return new ImportedModel
        {
            Meshes = meshes.ToArray(),
            Materials = materials,
            Rig = new ModelRig3D(joints, animations)
        };
    }

    private static ModelBone3D[] ReadBones(Mesh* mesh, Dictionary<nint, int> nodeIndices,
        Dictionary<string, int> nameIndices)
    {
        var bones = new ModelBone3D[mesh->MNumBones];
        for (uint i = 0; i < mesh->MNumBones; i++)
        {
            var bone = mesh->MBones[i];
            if (!nodeIndices.TryGetValue((nint)bone->MNode, out int joint))
                joint = ResolveNode(bone->MName.AsString, nameIndices);
            var weights = new ModelSkinWeight3D[bone->MNumWeights];
            for (uint j = 0; j < bone->MNumWeights; j++)
            {
                var entry = bone->MWeights[j];
                if (entry.MVertexId >= mesh->MNumVertices || !float.IsFinite(entry.MWeight) || entry.MWeight is < 0 or > 1)
                    throw new InvalidDataException($"Bone '{bone->MName.AsString}' contains an invalid vertex weight.");
                weights[j] = new ModelSkinWeight3D(checked((int)entry.MVertexId), entry.MWeight);
            }
            bones[i] = new ModelBone3D(joint, Matrix4x4.Transpose(bone->MOffsetMatrix), weights);
        }
        return bones;
    }

    private static ModelAnimation3D? ReadAnimation(Animation* source, uint index, Dictionary<string, int> nameIndices)
    {
        // Morph-only clips do not have joint data to sample.
        if (source->MNumChannels == 0)
            return null;

        // Unspecified rates have no source timebase. Use 25 ticks per second.
        double ticksPerSecond = source->MTicksPerSecond > 0 && double.IsFinite(source->MTicksPerSecond)
            ? source->MTicksPerSecond : 25;
        double firstTick = 0;
        double lastTick = Math.Max(0, double.IsFinite(source->MDuration) ? source->MDuration : 0);
        for (uint i = 0; i < source->MNumChannels; i++)
        {
            var channel = source->MChannels[i];
            for (uint key = 0; key < channel->MNumPositionKeys; key++)
                ExtendTimeline(channel->MPositionKeys[key].MTime, ref firstTick, ref lastTick);
            for (uint key = 0; key < channel->MNumRotationKeys; key++)
                ExtendTimeline(channel->MRotationKeys[key].MTime, ref firstTick, ref lastTick);
            for (uint key = 0; key < channel->MNumScalingKeys; key++)
                ExtendTimeline(channel->MScalingKeys[key].MTime, ref firstTick, ref lastTick);
        }

        double duration = (lastTick - firstTick) / ticksPerSecond;
        if (!double.IsFinite(duration))
            throw new InvalidDataException("The model contains an invalid animation duration.");

        var tracks = new ModelJointTrack3D[source->MNumChannels];
        for (uint i = 0; i < source->MNumChannels; i++)
        {
            var channel = source->MChannels[i];
            tracks[i] = new ModelJointTrack3D
            {
                Joint = ResolveNode(channel->MNodeName.AsString, nameIndices),
                Positions = ReadVectorKeys(channel->MPositionKeys, channel->MNumPositionKeys, firstTick, ticksPerSecond),
                Rotations = ReadRotationKeys(channel->MRotationKeys, channel->MNumRotationKeys, firstTick, ticksPerSecond),
                Scales = ReadVectorKeys(channel->MScalingKeys, channel->MNumScalingKeys, firstTick, ticksPerSecond)
            };
        }

        string name = source->MName.AsString;
        return new ModelAnimation3D
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Animation {index + 1}" : name,
            Duration = duration,
            Tracks = tracks
        };
    }

    private static void ExtendTimeline(double tick, ref double firstTick, ref double lastTick)
    {
        if (!double.IsFinite(tick))
            throw new InvalidDataException("The model contains an invalid animation key time.");
        firstTick = Math.Min(firstTick, tick);
        lastTick = Math.Max(lastTick, tick);
    }

    private static ModelVectorKey3D[] ReadVectorKeys(VectorKey* source, uint count, double start, double rate)
    {
        var keys = new ModelVectorKey3D[count];
        for (uint i = 0; i < count; i++)
        {
            var value = source[i].MValue;
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new InvalidDataException("The model contains an invalid animation vector.");
            keys[i] = new ModelVectorKey3D((source[i].MTime - start) / rate, value);
        }
        return keys.OrderBy(key => key.Time).ToArray();
    }

    private static ModelRotationKey3D[] ReadRotationKeys(QuatKey* source, uint count, double start, double rate)
    {
        var keys = new ModelRotationKey3D[count];
        for (uint i = 0; i < count; i++)
        {
            var value = source[i].MValue;
            var quaternion = new Quaternion(value.X, value.Y, value.Z, value.W);
            float length = quaternion.LengthSquared();
            if (!float.IsFinite(length) || length <= 0)
                throw new InvalidDataException("The model contains an invalid animation rotation.");
            keys[i] = new ModelRotationKey3D((source[i].MTime - start) / rate, Quaternion.Normalize(quaternion));
        }
        return keys.OrderBy(key => key.Time).ToArray();
    }

    private static int ResolveNode(string name, Dictionary<string, int> nameIndices)
    {
        if (nameIndices.TryGetValue(name, out int index) && index >= 0)
            return index;
        throw new InvalidDataException($"An animation or bone references an unknown or ambiguous scene node '{name}'.");
    }
}
