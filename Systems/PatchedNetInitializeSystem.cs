using Game;
using Game.Prefabs;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using CarLane = Game.Prefabs.CarLane;
using UtilityLane = Game.Prefabs.UtilityLane;
using TrackLane = Game.Prefabs.TrackLane;
using ParkingLane = Game.Prefabs.ParkingLane;
using PedestrianLane = Game.Prefabs.PedestrianLane;
using SecondaryLane = Game.Prefabs.SecondaryLane;
using RoadFlags = Game.Prefabs.RoadFlags;
using ElectricityConnection = Game.Prefabs.ElectricityConnection;
using WaterPipeConnection = Game.Prefabs.WaterPipeConnection;
using BepInEx.Logging;

namespace UnlimitedElevatedRoad.Systems
{
    // Token: 0x0200196B RID: 6507
    [CompilerGenerated]
    public class PatchedNetInitializeSystem : GameSystemBase
    {
        // Token: 0x060069F6 RID: 27126 RVA: 0x00487484 File Offset: 0x00485684
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            MaxElevatedLength = Plugin.MaxElevatedLength.Value;
            MaxElevatedLength = MaxElevatedLength > 16 ? MaxElevatedLength : 16;
            MaxPillarInterval = Plugin.MaxPillarInterval.Value;
            MaxPillarInterval = MaxPillarInterval > 16 ? MaxPillarInterval : 16;
            EnableNoPillar = Plugin.EnableNoPillar.Value;
            EnableUnlimitedHeight = Plugin.EnableUnlimitedHeight.Value;
            this.m_PrefabSystem = base.World.GetOrCreateSystemManaged<PrefabSystem>();
            this.m_PrefabQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<PrefabData>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadWrite<NetData>(),
                        ComponentType.ReadWrite<NetSectionData>(),
                        ComponentType.ReadWrite<NetPieceData>(),
                        ComponentType.ReadWrite<NetLaneData>()
                    },
                    None = new ComponentType[0]
                }
            });
            this.m_LaneQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<NetLaneData>()
            });
            base.RequireForUpdate(this.m_PrefabQuery);
            this.m_PathfindHeuristicData = new NativeValue<PathfindHeuristicData>(Allocator.Persistent);
        }

        // Token: 0x060069F7 RID: 27127 RVA: 0x00099AE3 File Offset: 0x00097CE3
        [Preserve]
        protected override void OnDestroy()
        {
            this.m_PathfindHeuristicData.Dispose();
            base.OnDestroy();
        }

        // Token: 0x060069F8 RID: 27128 RVA: 0x00099AF6 File Offset: 0x00097CF6
        public PathfindHeuristicData GetHeuristicData()
        {
            this.m_PathfindHeuristicDeps.Complete();
            return this.m_PathfindHeuristicData.value;
        }

        // Token: 0x060069F9 RID: 27129 RVA: 0x00487568 File Offset: 0x00485768
        private void AddSections(PrefabBase prefab, NetSectionInfo[] source, DynamicBuffer<NetGeometrySection> target, NetSectionFlags flags)
        {
            int2 @int = new int2(int.MaxValue, int.MinValue);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].m_Median)
                {
                    int y = i << 1;
                    @int.x = math.min(@int.x, y);
                    @int.y = math.max(@int.y, y);
                }
            }
            if (@int.Equals(new int2(2147483647, -2147483648)))
            {
                @int = source.Length - 1;
                flags |= NetSectionFlags.AlignCenter;
            }
            for (int j = 0; j < source.Length; j++)
            {
                NetSectionInfo netSectionInfo = source[j];
                NetGeometrySection elem = default(NetGeometrySection);
                elem.m_Section = this.m_PrefabSystem.GetEntity(netSectionInfo.m_Section);
                elem.m_Offset = netSectionInfo.m_Offset;
                elem.m_Flags = flags;
                NetSectionFlags netSectionFlags;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireAll, out elem.m_CompositionAll, out netSectionFlags);
                NetSectionFlags netSectionFlags2;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireAny, out elem.m_CompositionAny, out netSectionFlags2);
                NetSectionFlags netSectionFlags3;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireNone, out elem.m_CompositionNone, out netSectionFlags3);
                NetSectionFlags netSectionFlags4 = netSectionFlags | netSectionFlags2 | netSectionFlags3;
                if (netSectionFlags4 != (NetSectionFlags)0)
                {
                    COSystemBase.baseLog.ErrorFormat(prefab, "Net section ({0}: {1}) cannot require section flags: {2}", prefab.name, netSectionInfo.m_Section.name, netSectionFlags4);
                }
                if (netSectionInfo.m_Invert)
                {
                    elem.m_Flags |= NetSectionFlags.Invert;
                }
                if (netSectionInfo.m_Flip)
                {
                    elem.m_Flags |= (NetSectionFlags.FlipLanes | NetSectionFlags.FlipMesh);
                }
                int num = j << 1;
                if (num >= @int.x && num <= @int.y)
                {
                    elem.m_Flags |= NetSectionFlags.Median;
                }
                else if (num > @int.y)
                {
                    elem.m_Flags |= NetSectionFlags.Right;
                }
                else
                {
                    elem.m_Flags |= NetSectionFlags.Left;
                }
                target.Add(elem);
            }
        }

        // Token: 0x060069FA RID: 27130 RVA: 0x00487740 File Offset: 0x00485940
        [Preserve]
        protected override void OnUpdate()
        {
            NativeArray<ArchetypeChunk> chunks = this.m_PrefabQuery.ToArchetypeChunkArray(Allocator.TempJob);
            try
            {
                this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
                EntityTypeHandle _Unity_Entities_Entity_TypeHandle = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
                this.__TypeHandle.__Game_Prefabs_PrefabData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PrefabData> _Game_Prefabs_PrefabData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PrefabData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetData> _Game_Prefabs_NetData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetPieceData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetPieceData> _Game_Prefabs_NetPieceData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetPieceData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetGeometryData> _Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PlaceableNetData> _Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<MarkerNetData> _Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<LocalConnectData> _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetLaneData> _Game_Prefabs_NetLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetLaneGeometryData> _Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_CarLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<CarLaneData> _Game_Prefabs_CarLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_CarLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<TrackLaneData> _Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<UtilityLaneData> _Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<ParkingLaneData> _Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PedestrianLaneData> _Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<SecondaryLaneData> _Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetCrosswalkData> _Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<RoadData> _Game_Prefabs_RoadData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_TrackData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<TrackData> _Game_Prefabs_TrackData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_TrackData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_WaterwayData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<WaterwayData> _Game_Prefabs_WaterwayData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_WaterwayData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_PathwayData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PathwayData> _Game_Prefabs_PathwayData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PathwayData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<TaxiwayData> _Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_PowerLineData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PowerLineData> _Game_Prefabs_PowerLineData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PowerLineData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_PipelineData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<PipelineData> _Game_Prefabs_PipelineData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_PipelineData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_FenceData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<FenceData> _Game_Prefabs_FenceData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_FenceData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<EditorContainerData> _Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<ElectricityConnectionData> _Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<WaterPipeConnectionData> _Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_BridgeData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<BridgeData> _Game_Prefabs_BridgeData_RO_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_BridgeData_RO_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<SpawnableObjectData> _Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                ComponentTypeHandle<NetTerrainData> _Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle = this.__TypeHandle.__Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetSubSection_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetSubSection> _Game_Prefabs_NetSubSection_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetSubSection_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetSectionPiece> _Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetPieceLane_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetPieceLane> _Game_Prefabs_NetPieceLane_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetPieceLane_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetPieceArea_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetPieceArea> _Game_Prefabs_NetPieceArea_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetPieceArea_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetPieceObject_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetPieceObject> _Game_Prefabs_NetPieceObject_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetPieceObject_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetGeometrySection> _Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetGeometryEdgeState> _Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<NetGeometryNodeState> _Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_SubObject_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<SubObject> _Game_Prefabs_SubObject_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_SubObject_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_SubMesh_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<SubMesh> _Game_Prefabs_SubMesh_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_SubMesh_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_FixedNetElement_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<FixedNetElement> _Game_Prefabs_FixedNetElement_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_FixedNetElement_RW_BufferTypeHandle;
                this.__TypeHandle.__Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
                BufferTypeHandle<AuxiliaryNetLane> _Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle = this.__TypeHandle.__Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle;
                base.CompleteDependency();
                for (int i = 0; i < chunks.Length; i++)
                {
                    ArchetypeChunk archetypeChunk = chunks[i];
                    NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(_Unity_Entities_Entity_TypeHandle);
                    NativeArray<PrefabData> nativeArray2 = archetypeChunk.GetNativeArray<PrefabData>(ref _Game_Prefabs_PrefabData_RO_ComponentTypeHandle);
                    bool flag = archetypeChunk.Has<MarkerNetData>(ref _Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle);
                    bool flag2 = archetypeChunk.Has<BridgeData>(ref _Game_Prefabs_BridgeData_RO_ComponentTypeHandle);
                    NativeArray<NetData> nativeArray3 = archetypeChunk.GetNativeArray<NetData>(ref _Game_Prefabs_NetData_RW_ComponentTypeHandle);
                    NativeArray<NetGeometryData> nativeArray4 = archetypeChunk.GetNativeArray<NetGeometryData>(ref _Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle);
                    NativeArray<PlaceableNetData> nativeArray5 = archetypeChunk.GetNativeArray<PlaceableNetData>(ref _Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle);
                    if (nativeArray4.Length != 0)
                    {
                        BufferAccessor<NetGeometrySection> bufferAccessor = archetypeChunk.GetBufferAccessor<NetGeometrySection>(ref _Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle);
                        for (int j = 0; j < nativeArray4.Length; j++)
                        {
                            Entity entity = nativeArray[j];
                            NetGeometryPrefab prefab = this.m_PrefabSystem.GetPrefab<NetGeometryPrefab>(nativeArray2[j]);
                            NetGeometryData value = nativeArray4[j];
                            DynamicBuffer<NetGeometrySection> target = bufferAccessor[j];
                            value.m_EdgeLengthRange.max = (float)MaxElevatedLength;
                            value.m_ElevatedLength = (float)MaxPillarInterval;
                            logger.LogMessage("Max node interval changed from 200 to " + MaxPillarInterval);
                            logger.LogMessage("Max pillar interval changed from 80 to " + MaxPillarInterval);
                            value.m_MaxSlopeSteepness = math.select(prefab.m_MaxSlopeSteepness, 0f, prefab.m_MaxSlopeSteepness < 0.001f);
                            value.m_ElevationLimit = 4f;
                            if (prefab.m_AggregateType != null)
                            {
                                value.m_AggregateType = this.m_PrefabSystem.GetEntity(prefab.m_AggregateType);
                            }
                            if (flag)
                            {
                                value.m_Flags |= GeometryFlags.Marker;
                            }
                            this.AddSections(prefab, prefab.m_Sections, target, (NetSectionFlags)0);
                            UndergroundNetSections component = prefab.GetComponent<UndergroundNetSections>();
                            if (component != null)
                            {
                                this.AddSections(prefab, component.m_Sections, target, NetSectionFlags.Underground);
                            }
                            if (!EnableNoPillar)
                            {
                                OverheadNetSections component2 = prefab.GetComponent<OverheadNetSections>();
                                if (component2 != null)
                                {
                                    this.AddSections(prefab, component2.m_Sections, target, NetSectionFlags.Overhead);
                                }
                            }
                            switch (prefab.m_InvertMode)
                            {
                                case CompositionInvertMode.InvertLefthandTraffic:
                                    value.m_Flags |= GeometryFlags.InvertCompositionHandedness;
                                    break;
                                case CompositionInvertMode.FlipLefthandTraffic:
                                    value.m_Flags |= GeometryFlags.FlipCompositionHandedness;
                                    break;
                                case CompositionInvertMode.InvertRighthandTraffic:
                                    value.m_Flags |= (GeometryFlags.IsLefthanded | GeometryFlags.InvertCompositionHandedness);
                                    break;
                                case CompositionInvertMode.FlipRighthandTraffic:
                                    value.m_Flags |= (GeometryFlags.IsLefthanded | GeometryFlags.FlipCompositionHandedness);
                                    break;
                            }
                            nativeArray4[j] = value;
                        }
                        BufferAccessor<NetGeometryEdgeState> bufferAccessor2 = archetypeChunk.GetBufferAccessor<NetGeometryEdgeState>(ref _Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle);
                        BufferAccessor<NetGeometryNodeState> bufferAccessor3 = archetypeChunk.GetBufferAccessor<NetGeometryNodeState>(ref _Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle);
                        for (int k = 0; k < nativeArray4.Length; k++)
                        {
                            NetGeometryPrefab prefab2 = this.m_PrefabSystem.GetPrefab<NetGeometryPrefab>(nativeArray2[k]);
                            DynamicBuffer<NetGeometryEdgeState> dynamicBuffer = bufferAccessor2[k];
                            DynamicBuffer<NetGeometryNodeState> dynamicBuffer2 = bufferAccessor3[k];
                            if (prefab2.m_EdgeStates != null)
                            {
                                for (int l = 0; l < prefab2.m_EdgeStates.Length; l++)
                                {
                                    NetEdgeStateInfo netEdgeStateInfo = prefab2.m_EdgeStates[l];
                                    NetGeometryEdgeState elem = default(NetGeometryEdgeState);
                                    NetSectionFlags netSectionFlags;
                                    NetCompositionHelpers.GetRequirementFlags(netEdgeStateInfo.m_RequireAll, out elem.m_CompositionAll, out netSectionFlags);
                                    NetSectionFlags netSectionFlags2;
                                    NetCompositionHelpers.GetRequirementFlags(netEdgeStateInfo.m_RequireAny, out elem.m_CompositionAny, out netSectionFlags2);
                                    NetSectionFlags netSectionFlags3;
                                    NetCompositionHelpers.GetRequirementFlags(netEdgeStateInfo.m_RequireNone, out elem.m_CompositionNone, out netSectionFlags3);
                                    NetSectionFlags netSectionFlags4;
                                    NetCompositionHelpers.GetRequirementFlags(netEdgeStateInfo.m_SetState, out elem.m_State, out netSectionFlags4);
                                    NetSectionFlags netSectionFlags5 = netSectionFlags | netSectionFlags2 | netSectionFlags3 | netSectionFlags4;
                                    if (netSectionFlags5 != (NetSectionFlags)0)
                                    {
                                        COSystemBase.baseLog.ErrorFormat(prefab2, "Net edge state ({0}) cannot require/set section flags: {1}", prefab2.name, netSectionFlags5);
                                    }
                                    dynamicBuffer.Add(elem);
                                }
                            }
                            if (prefab2.m_NodeStates != null)
                            {
                                for (int m = 0; m < prefab2.m_NodeStates.Length; m++)
                                {
                                    NetNodeStateInfo netNodeStateInfo = prefab2.m_NodeStates[m];
                                    NetGeometryNodeState elem2 = default(NetGeometryNodeState);
                                    NetSectionFlags netSectionFlags6;
                                    NetCompositionHelpers.GetRequirementFlags(netNodeStateInfo.m_RequireAll, out elem2.m_CompositionAll, out netSectionFlags6);
                                    NetSectionFlags netSectionFlags7;
                                    NetCompositionHelpers.GetRequirementFlags(netNodeStateInfo.m_RequireAny, out elem2.m_CompositionAny, out netSectionFlags7);
                                    NetSectionFlags netSectionFlags8;
                                    NetCompositionHelpers.GetRequirementFlags(netNodeStateInfo.m_RequireNone, out elem2.m_CompositionNone, out netSectionFlags8);
                                    NetSectionFlags netSectionFlags9;
                                    NetCompositionHelpers.GetRequirementFlags(netNodeStateInfo.m_SetState, out elem2.m_State, out netSectionFlags9);
                                    NetSectionFlags netSectionFlags10 = netSectionFlags6 | netSectionFlags7 | netSectionFlags8 | netSectionFlags9;
                                    if (netSectionFlags10 != (NetSectionFlags)0)
                                    {
                                        COSystemBase.baseLog.ErrorFormat(prefab2, "Net node state ({0}) cannot require/set section flags: {1}", prefab2.name, netSectionFlags10);
                                    }
                                    elem2.m_MatchType = netNodeStateInfo.m_MatchType;
                                    dynamicBuffer2.Add(elem2);
                                }
                            }
                        }
                    }
                    for (int n = 0; n < nativeArray5.Length; n++)
                    {
                        NetPrefab prefab3 = this.m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[n]);
                        PlaceableNetData placeableNetData = nativeArray5[n];
                        placeableNetData.m_SnapDistance = 8f;
                        PlaceableNet component3 = prefab3.GetComponent<PlaceableNet>();
                        if (component3 != null)
                        {
                            if (EnableUnlimitedHeight)
                            {
                                if (component3.m_UndergroundPrefab == null && component3.m_ElevationRange != default(Bounds1))
                                {
                                    if(component3.m_ElevationRange.max >= 20f)
                                    {
                                        logger.LogMessage("Elevation max height changed from " + component3.m_ElevationRange.max + " to 1000.");
                                        component3.m_ElevationRange.max = 1000f;
                                    }
                                }
                            }
                            placeableNetData.m_ElevationRange = component3.m_ElevationRange;
                            placeableNetData.m_XPReward = component3.m_XPReward;
                            if (component3.m_UndergroundPrefab != null)
                            {
                                placeableNetData.m_UndergroundPrefab = this.m_PrefabSystem.GetEntity(component3.m_UndergroundPrefab);
                            }
                            if (component3.m_AllowParallelMode)
                            {
                                placeableNetData.m_PlacementFlags |= PlacementFlags.AllowParallel;
                            }
                            
                        }
                        NetUpgrade component4 = prefab3.GetComponent<NetUpgrade>();
                        if (component4 != null)
                        {
                            NetSectionFlags netSectionFlags11;
                            NetCompositionHelpers.GetRequirementFlags(component4.m_SetState, out placeableNetData.m_SetUpgradeFlags, out netSectionFlags11);
                            NetSectionFlags netSectionFlags12;
                            NetCompositionHelpers.GetRequirementFlags(component4.m_UnsetState, out placeableNetData.m_UnsetUpgradeFlags, out netSectionFlags12);
                            placeableNetData.m_PlacementFlags |= PlacementFlags.IsUpgrade;
                            if (!component4.m_Standalone)
                            {
                                placeableNetData.m_PlacementFlags |= PlacementFlags.UpgradeOnly;
                            }
                            if (component4.m_Underground)
                            {
                                placeableNetData.m_PlacementFlags |= PlacementFlags.UndergroundUpgrade;
                            }
                            if (((placeableNetData.m_SetUpgradeFlags | placeableNetData.m_UnsetUpgradeFlags) & CompositionFlags.nodeMask) != default(CompositionFlags))
                            {
                                placeableNetData.m_PlacementFlags |= PlacementFlags.NodeUpgrade;
                            }
                            NetSectionFlags netSectionFlags13 = netSectionFlags11 | netSectionFlags12;
                            if (netSectionFlags13 != (NetSectionFlags)0)
                            {
                                COSystemBase.baseLog.ErrorFormat(prefab3, "PlaceableNet ({0}) cannot upgrade section flags: {1}", prefab3.name, netSectionFlags13);
                            }
                        }
                        nativeArray5[n] = placeableNetData;
                    }
                    BufferAccessor<SubObject> bufferAccessor4 = archetypeChunk.GetBufferAccessor<SubObject>(ref _Game_Prefabs_SubObject_RW_BufferTypeHandle);
                    for (int num = 0; num < bufferAccessor4.Length; num++)
                    {
                        NetSubObjects component5 = this.m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[num]).GetComponent<NetSubObjects>();
                        bool flag3 = false;
                        NetGeometryData value2 = default(NetGeometryData);
                        if (nativeArray4.Length != 0)
                        {
                            value2 = nativeArray4[num];
                        }
                        
                        DynamicBuffer<SubObject> dynamicBuffer3 = bufferAccessor4[num];
                        for (int num2 = 0; num2 < component5.m_SubObjects.Length; num2++)
                        {
                            NetSubObjectInfo netSubObjectInfo = component5.m_SubObjects[num2];
                            ObjectPrefab @object = netSubObjectInfo.m_Object;
                            SubObject subObject = default(SubObject);
                            subObject.m_Prefab = this.m_PrefabSystem.GetEntity(@object);
                            subObject.m_Position = netSubObjectInfo.m_Position;
                            subObject.m_Rotation = netSubObjectInfo.m_Rotation;
                            subObject.m_Probability = 100;
                            bool valid = !EnableNoPillar;
                            switch (netSubObjectInfo.m_Placement)
                            {
                                case NetObjectPlacement.EdgeEndsOrNode:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine);
                                    break;
                                case NetObjectPlacement.EdgeMiddle:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.MiddlePlacement);
                                    if (base.EntityManager.HasComponent<PillarData>(subObject.m_Prefab))
                                    {
                                        value2.m_Flags |= GeometryFlags.MiddlePillars;
                                    }
                                    break;
                                case NetObjectPlacement.EdgeEnds:
                                    subObject.m_Flags |= SubObjectFlags.EdgePlacement;
                                    break;
                                case NetObjectPlacement.CourseStart:
                                    subObject.m_Flags |= (SubObjectFlags.CoursePlacement | SubObjectFlags.StartPlacement);
                                    if (!flag3)
                                    {
                                        subObject.m_Flags |= SubObjectFlags.MakeOwner;
                                        value2.m_Flags |= GeometryFlags.SubOwner;
                                        flag3 = true;
                                    }
                                    break;
                                case NetObjectPlacement.CourseEnd:
                                    subObject.m_Flags |= (SubObjectFlags.CoursePlacement | SubObjectFlags.EndPlacement);
                                    if (!flag3)
                                    {
                                        subObject.m_Flags |= SubObjectFlags.MakeOwner;
                                        value2.m_Flags |= GeometryFlags.SubOwner;
                                        flag3 = true;
                                    }
                                    break;
                                case NetObjectPlacement.NodeBeforeFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.NodeBetweenFixedSegment:
                                    subObject.m_Flags |= SubObjectFlags.FixedPlacement;
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.NodeAfterFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeMiddleFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.MiddlePlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    if (base.EntityManager.HasComponent<PillarData>(subObject.m_Prefab))
                                    {
                                        value2.m_Flags |= GeometryFlags.MiddlePillars;
                                    }
                                    break;
                                case NetObjectPlacement.EdgeEndsFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeStartFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeEndFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeEndsOrNodeFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeStartOrNodeFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                                case NetObjectPlacement.EdgeEndOrNodeFixedSegment:
                                    subObject.m_Flags |= (SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement);
                                    subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                    break;
                            }
                            if (netSubObjectInfo.m_AnchorTop)
                            {
                                subObject.m_Flags |= SubObjectFlags.AnchorTop;
                            }
                            if (netSubObjectInfo.m_AnchorCenter)
                            {
                                subObject.m_Flags |= SubObjectFlags.AnchorCenter;
                            }
                            if (netSubObjectInfo.m_RequireElevated)
                            {
                                subObject.m_Flags |= SubObjectFlags.RequireElevated;
                            }
                            if (netSubObjectInfo.m_RequireOutsideConnection)
                            {
                                subObject.m_Flags |= SubObjectFlags.RequireOutsideConnection;
                                valid = true;
                            }
                            if (netSubObjectInfo.m_RequireDeadEnd)
                            {
                                subObject.m_Flags |= SubObjectFlags.RequireDeadEnd;
                            }
                            if (netSubObjectInfo.m_RequireOrphan)
                            {
                                subObject.m_Flags |= SubObjectFlags.RequireOrphan;
                            }
                            if(valid) dynamicBuffer3.Add(subObject);
                        }
                        if (nativeArray4.Length != 0)
                        {
                            nativeArray4[num] = value2;
                        }
                    }
                    BufferAccessor<NetSectionPiece> bufferAccessor5 = archetypeChunk.GetBufferAccessor<NetSectionPiece>(ref _Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle);
                    if (bufferAccessor5.Length != 0)
                    {
                        BufferAccessor<NetSubSection> bufferAccessor6 = archetypeChunk.GetBufferAccessor<NetSubSection>(ref _Game_Prefabs_NetSubSection_RW_BufferTypeHandle);
                        for (int num3 = 0; num3 < bufferAccessor5.Length; num3++)
                        {
                            NetSectionPrefab prefab4 = this.m_PrefabSystem.GetPrefab<NetSectionPrefab>(nativeArray2[num3]);
                            DynamicBuffer<NetSubSection> dynamicBuffer4 = bufferAccessor6[num3];
                            DynamicBuffer<NetSectionPiece> dynamicBuffer5 = bufferAccessor5[num3];
                            if (prefab4.m_SubSections != null)
                            {
                                for (int num4 = 0; num4 < prefab4.m_SubSections.Length; num4++)
                                {
                                    NetSubSectionInfo netSubSectionInfo = prefab4.m_SubSections[num4];
                                    NetSubSection elem3 = default(NetSubSection);
                                    elem3.m_SubSection = this.m_PrefabSystem.GetEntity(netSubSectionInfo.m_Section);
                                    NetCompositionHelpers.GetRequirementFlags(netSubSectionInfo.m_RequireAll, out elem3.m_CompositionAll, out elem3.m_SectionAll);
                                    NetCompositionHelpers.GetRequirementFlags(netSubSectionInfo.m_RequireAny, out elem3.m_CompositionAny, out elem3.m_SectionAny);
                                    NetCompositionHelpers.GetRequirementFlags(netSubSectionInfo.m_RequireNone, out elem3.m_CompositionNone, out elem3.m_SectionNone);
                                    dynamicBuffer4.Add(elem3);
                                }
                            }
                            if (prefab4.m_Pieces != null)
                            {
                                for (int num5 = 0; num5 < prefab4.m_Pieces.Length; num5++)
                                {
                                    NetPieceInfo netPieceInfo = prefab4.m_Pieces[num5];
                                    NetSectionPiece elem4 = default(NetSectionPiece);
                                    elem4.m_Piece = this.m_PrefabSystem.GetEntity(netPieceInfo.m_Piece);
                                    NetCompositionHelpers.GetRequirementFlags(netPieceInfo.m_RequireAll, out elem4.m_CompositionAll, out elem4.m_SectionAll);
                                    NetCompositionHelpers.GetRequirementFlags(netPieceInfo.m_RequireAny, out elem4.m_CompositionAny, out elem4.m_SectionAny);
                                    NetCompositionHelpers.GetRequirementFlags(netPieceInfo.m_RequireNone, out elem4.m_CompositionNone, out elem4.m_SectionNone);
                                    NetPieceLayer layer = netPieceInfo.m_Piece.m_Layer;
                                    if (layer != NetPieceLayer.Surface)
                                    {
                                        if (layer == NetPieceLayer.Side)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.Side;
                                        }
                                    }
                                    else
                                    {
                                        elem4.m_Flags |= NetPieceFlags.Surface;
                                    }
                                    if (netPieceInfo.m_Piece.meshCount != 0)
                                    {
                                        elem4.m_Flags |= NetPieceFlags.HasMesh;
                                    }
                                    NetDividerPiece component6 = netPieceInfo.m_Piece.GetComponent<NetDividerPiece>();
                                    if (component6 != null)
                                    {
                                        if (component6.m_PreserveShape)
                                        {
                                            elem4.m_Flags |= (NetPieceFlags.PreserveShape | NetPieceFlags.DisableTiling);
                                        }
                                        if (component6.m_BlockTraffic)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.BlockTraffic;
                                        }
                                        if (component6.m_BlockCrosswalk)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.BlockCrosswalk;
                                        }
                                    }
                                    NetPieceTiling component7 = netPieceInfo.m_Piece.GetComponent<NetPieceTiling>();
                                    if (component7 != null && component7.m_DisableTextureTiling)
                                    {
                                        elem4.m_Flags |= NetPieceFlags.DisableTiling;
                                    }
                                    MovePieceVertices component8 = netPieceInfo.m_Piece.GetComponent<MovePieceVertices>();
                                    if (component8 != null)
                                    {
                                        if (component8.m_LowerBottomToTerrain)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.LowerBottomToTerrain;
                                        }
                                        if (component8.m_RaiseTopToTerrain)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.RaiseTopToTerrain;
                                        }
                                        if (component8.m_SmoothTopNormal)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.SmoothTopNormal;
                                        }
                                    }
                                    AsymmetricPieceMesh component9 = netPieceInfo.m_Piece.GetComponent<AsymmetricPieceMesh>();
                                    if (component9 != null)
                                    {
                                        if (component9.m_Sideways)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.AsymmetricMeshX;
                                        }
                                        if (component9.m_Lengthwise)
                                        {
                                            elem4.m_Flags |= NetPieceFlags.AsymmetricMeshZ;
                                        }
                                    }
                                    elem4.m_Offset = netPieceInfo.m_Offset;
                                    dynamicBuffer5.Add(elem4);
                                }
                            }
                        }
                    }
                    NativeArray<NetPieceData> nativeArray6 = archetypeChunk.GetNativeArray<NetPieceData>(ref _Game_Prefabs_NetPieceData_RW_ComponentTypeHandle);
                    if (nativeArray6.Length != 0)
                    {
                        BufferAccessor<NetPieceLane> bufferAccessor7 = archetypeChunk.GetBufferAccessor<NetPieceLane>(ref _Game_Prefabs_NetPieceLane_RW_BufferTypeHandle);
                        BufferAccessor<NetPieceArea> bufferAccessor8 = archetypeChunk.GetBufferAccessor<NetPieceArea>(ref _Game_Prefabs_NetPieceArea_RW_BufferTypeHandle);
                        BufferAccessor<NetPieceObject> bufferAccessor9 = archetypeChunk.GetBufferAccessor<NetPieceObject>(ref _Game_Prefabs_NetPieceObject_RW_BufferTypeHandle);
                        NativeArray<NetCrosswalkData> nativeArray7 = archetypeChunk.GetNativeArray<NetCrosswalkData>(ref _Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle);
                        NativeArray<NetTerrainData> nativeArray8 = archetypeChunk.GetNativeArray<NetTerrainData>(ref _Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle);
                        for (int num6 = 0; num6 < nativeArray6.Length; num6++)
                        {
                            NetPiecePrefab prefab5 = this.m_PrefabSystem.GetPrefab<NetPiecePrefab>(nativeArray2[num6]);
                            NetPieceData value3 = nativeArray6[num6];
                            value3.m_HeightRange = prefab5.m_HeightRange;
                            value3.m_SurfaceHeights = prefab5.m_SurfaceHeights;
                            value3.m_Width = prefab5.m_Width;
                            value3.m_Length = prefab5.m_Length;
                            value3.m_WidthOffset = prefab5.m_WidthOffset;
                            value3.m_NodeOffset = prefab5.m_NodeOffset;
                            if (bufferAccessor7.Length != 0)
                            {
                                NetPieceLanes component10 = prefab5.GetComponent<NetPieceLanes>();
                                if (component10.m_Lanes != null)
                                {
                                    DynamicBuffer<NetPieceLane> dynamicBuffer6 = bufferAccessor7[num6];
                                    for (int num7 = 0; num7 < component10.m_Lanes.Length; num7++)
                                    {
                                        NetLaneInfo netLaneInfo = component10.m_Lanes[num7];
                                        NetPieceLane elem5 = default(NetPieceLane);
                                        elem5.m_Lane = this.m_PrefabSystem.GetEntity(netLaneInfo.m_Lane);
                                        elem5.m_Position = netLaneInfo.m_Position;
                                        if (netLaneInfo.m_FindAnchor)
                                        {
                                            elem5.m_ExtraFlags |= LaneFlags.FindAnchor;
                                        }
                                        dynamicBuffer6.Add(elem5);
                                    }
                                    if (dynamicBuffer6.Length > 1)
                                    {
                                        dynamicBuffer6.AsNativeArray().Sort<NetPieceLane>();
                                    }
                                }
                            }
                            if (bufferAccessor8.Length != 0)
                            {
                                DynamicBuffer<NetPieceArea> dynamicBuffer7 = bufferAccessor8[num6];
                                BuildableNetPiece component11 = prefab5.GetComponent<BuildableNetPiece>();
                                if (component11 != null)
                                {
                                    dynamicBuffer7.Add(new NetPieceArea
                                    {
                                        m_Flags = (component11.m_AllowOnBridge ? NetAreaFlags.Buildable : (NetAreaFlags.Buildable | NetAreaFlags.NoBridge)),
                                        m_Position = component11.m_Position,
                                        m_Width = component11.m_Width,
                                        m_SnapPosition = component11.m_SnapPosition,
                                        m_SnapWidth = component11.m_SnapWidth
                                    });
                                }
                                if (dynamicBuffer7.Length > 1)
                                {
                                    dynamicBuffer7.AsNativeArray().Sort<NetPieceArea>();
                                }
                            }
                            if (bufferAccessor9.Length != 0)
                            {
                                DynamicBuffer<NetPieceObject> dynamicBuffer8 = bufferAccessor9[num6];
                                NetPieceObjects component12 = prefab5.GetComponent<NetPieceObjects>();
                                if (component12 != null)
                                {
                                    dynamicBuffer8.ResizeUninitialized(component12.m_PieceObjects.Length);
                                    for (int num8 = 0; num8 < component12.m_PieceObjects.Length; num8++)
                                    {
                                        NetPieceObjectInfo netPieceObjectInfo = component12.m_PieceObjects[num8];
                                        NetPieceObject value4 = default(NetPieceObject);
                                        value4.m_Prefab = this.m_PrefabSystem.GetEntity(netPieceObjectInfo.m_Object);
                                        value4.m_Position = netPieceObjectInfo.m_Position;
                                        value4.m_Offset = netPieceObjectInfo.m_Offset;
                                        value4.m_Spacing = netPieceObjectInfo.m_Spacing;
                                        value4.m_UseCurveRotation = netPieceObjectInfo.m_UseCurveRotation;
                                        value4.m_MinLength = netPieceObjectInfo.m_MinLength;
                                        value4.m_Probability = math.select(netPieceObjectInfo.m_Probability, 100, netPieceObjectInfo.m_Probability == 0);
                                        value4.m_CurveOffsetRange = netPieceObjectInfo.m_CurveOffsetRange;
                                        value4.m_Rotation = netPieceObjectInfo.m_Rotation;
                                        NetCompositionHelpers.GetRequirementFlags(netPieceObjectInfo.m_RequireAll, out value4.m_CompositionAll, out value4.m_SectionAll);
                                        NetCompositionHelpers.GetRequirementFlags(netPieceObjectInfo.m_RequireAny, out value4.m_CompositionAny, out value4.m_SectionAny);
                                        NetCompositionHelpers.GetRequirementFlags(netPieceObjectInfo.m_RequireNone, out value4.m_CompositionNone, out value4.m_SectionNone);
                                        if (netPieceObjectInfo.m_FlipWhenInverted)
                                        {
                                            value4.m_Flags |= SubObjectFlags.FlipInverted;
                                        }
                                        if (netPieceObjectInfo.m_EvenSpacing)
                                        {
                                            value4.m_Flags |= SubObjectFlags.EvenSpacing;
                                        }
                                        if (netPieceObjectInfo.m_SpacingOverride)
                                        {
                                            value4.m_Flags |= SubObjectFlags.SpacingOverride;
                                        }
                                        dynamicBuffer8[num8] = value4;
                                    }
                                }
                            }
                            if (nativeArray7.Length != 0)
                            {
                                NetPieceCrosswalk component13 = prefab5.GetComponent<NetPieceCrosswalk>();
                                nativeArray7[num6] = new NetCrosswalkData
                                {
                                    m_Lane = this.m_PrefabSystem.GetEntity(component13.m_Lane),
                                    m_Start = component13.m_Start,
                                    m_End = component13.m_End
                                };
                            }
                            if (nativeArray8.Length != 0)
                            {
                                NetTerrainPiece component14 = prefab5.GetComponent<NetTerrainPiece>();
                                nativeArray8[num6] = new NetTerrainData
                                {
                                    m_WidthOffset = component14.m_WidthOffset,
                                    m_ClipHeightOffset = component14.m_ClipHeightOffset,
                                    m_MinHeightOffset = component14.m_MinHeightOffset,
                                    m_MaxHeightOffset = component14.m_MaxHeightOffset
                                };
                            }
                            nativeArray6[num6] = value3;
                        }
                    }
                    NativeArray<NetLaneData> nativeArray9 = archetypeChunk.GetNativeArray<NetLaneData>(ref _Game_Prefabs_NetLaneData_RW_ComponentTypeHandle);
                    if (nativeArray9.Length != 0)
                    {
                        NativeArray<ParkingLaneData> nativeArray10 = archetypeChunk.GetNativeArray<ParkingLaneData>(ref _Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle);
                        NativeArray<CarLaneData> nativeArray11 = archetypeChunk.GetNativeArray<CarLaneData>(ref _Game_Prefabs_CarLaneData_RW_ComponentTypeHandle);
                        NativeArray<TrackLaneData> nativeArray12 = archetypeChunk.GetNativeArray<TrackLaneData>(ref _Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle);
                        NativeArray<UtilityLaneData> nativeArray13 = archetypeChunk.GetNativeArray<UtilityLaneData>(ref _Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle);
                        NativeArray<SecondaryLaneData> nativeArray14 = archetypeChunk.GetNativeArray<SecondaryLaneData>(ref _Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle);
                        BufferAccessor<AuxiliaryNetLane> bufferAccessor10 = archetypeChunk.GetBufferAccessor<AuxiliaryNetLane>(ref _Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle);
                        bool flag4 = archetypeChunk.Has<PedestrianLaneData>(ref _Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle);
                        for (int num9 = 0; num9 < nativeArray9.Length; num9++)
                        {
                            NetLanePrefab prefab6 = this.m_PrefabSystem.GetPrefab<NetLanePrefab>(nativeArray2[num9]);
                            NetLaneData value5 = nativeArray9[num9];
                            if (prefab6.m_PathfindPrefab != null)
                            {
                                value5.m_PathfindPrefab = this.m_PrefabSystem.GetEntity(prefab6.m_PathfindPrefab);
                            }
                            if (nativeArray11.Length != 0)
                            {
                                CarLane component15 = prefab6.GetComponent<CarLane>();
                                value5.m_Flags |= LaneFlags.Road;
                                value5.m_Width = component15.m_Width;
                                if (component15.m_StartingLane)
                                {
                                    value5.m_Flags |= LaneFlags.DisconnectedStart;
                                }
                                if (component15.m_EndingLane)
                                {
                                    value5.m_Flags |= LaneFlags.DisconnectedEnd;
                                }
                                if (component15.m_Twoway)
                                {
                                    value5.m_Flags |= LaneFlags.Twoway;
                                }
                                if (component15.m_BusLane)
                                {
                                    value5.m_Flags |= LaneFlags.PublicOnly;
                                }
                                if (component15.m_RoadType == RoadTypes.Watercraft)
                                {
                                    value5.m_Flags |= LaneFlags.OnWater;
                                }
                                CarLaneData value6 = nativeArray11[num9];
                                if (component15.m_NotTrackLane != null)
                                {
                                    value6.m_NotTrackLanePrefab = this.m_PrefabSystem.GetEntity(component15.m_NotTrackLane);
                                }
                                if (component15.m_NotBusLane != null)
                                {
                                    value6.m_NotBusLanePrefab = this.m_PrefabSystem.GetEntity(component15.m_NotBusLane);
                                }
                                value6.m_RoadTypes = component15.m_RoadType;
                                value6.m_MaxSize = component15.m_MaxSize;
                                nativeArray11[num9] = value6;
                            }
                            if (nativeArray12.Length != 0)
                            {
                                TrackLane component16 = prefab6.GetComponent<TrackLane>();
                                value5.m_Flags |= LaneFlags.Track;
                                value5.m_Width = component16.m_Width;
                                if (component16.m_Twoway)
                                {
                                    value5.m_Flags |= LaneFlags.Twoway;
                                }
                                TrackLaneData value7 = nativeArray12[num9];
                                if (component16.m_FallbackLane != null)
                                {
                                    value7.m_FallbackPrefab = this.m_PrefabSystem.GetEntity(component16.m_FallbackLane);
                                }
                                if (component16.m_EndObject != null)
                                {
                                    value7.m_EndObjectPrefab = this.m_PrefabSystem.GetEntity(component16.m_EndObject);
                                }
                                value7.m_TrackTypes = component16.m_TrackType;
                                value7.m_MaxCurviness = math.radians(component16.m_MaxCurviness);
                                nativeArray12[num9] = value7;
                            }
                            if (nativeArray13.Length != 0)
                            {
                                UtilityLane component17 = prefab6.GetComponent<UtilityLane>();
                                value5.m_Flags |= LaneFlags.Utility;
                                value5.m_Width = component17.m_Width;
                                if (component17.m_Underground)
                                {
                                    value5.m_Flags |= LaneFlags.Underground;
                                }
                                UtilityLaneData value8 = nativeArray13[num9];
                                if (component17.m_LocalConnectionLane != null)
                                {
                                    value8.m_LocalConnectionPrefab = this.m_PrefabSystem.GetEntity(component17.m_LocalConnectionLane);
                                }
                                if (component17.m_LocalConnectionLane2 != null)
                                {
                                    value8.m_LocalConnectionPrefab2 = this.m_PrefabSystem.GetEntity(component17.m_LocalConnectionLane2);
                                }
                                if (component17.m_NodeObject != null)
                                {
                                    value8.m_NodeObjectPrefab = this.m_PrefabSystem.GetEntity(component17.m_NodeObject);
                                }
                                value8.m_VisualCapacity = component17.m_VisualCapacity;
                                value8.m_Hanging = component17.m_Hanging;
                                value8.m_UtilityTypes = component17.m_UtilityType;
                                nativeArray13[num9] = value8;
                            }
                            if (nativeArray10.Length != 0)
                            {
                                ParkingLane component18 = prefab6.GetComponent<ParkingLane>();
                                value5.m_Flags |= LaneFlags.Parking;
                                ParkingLaneData parkingLaneData = nativeArray10[num9];
                                parkingLaneData.m_SlotSize = math.select(component18.m_SlotSize, 0f, component18.m_SlotSize < 0.001f);
                                parkingLaneData.m_SlotAngle = math.radians(math.clamp(component18.m_SlotAngle, 0f, 90f));
                                parkingLaneData.m_MaxCarLength = parkingLaneData.m_SlotSize.y;
                                float2 @float = new float2(math.cos(parkingLaneData.m_SlotAngle), math.sin(parkingLaneData.m_SlotAngle));
                                if (@float.y < 0.001f)
                                {
                                    parkingLaneData.m_SlotInterval = parkingLaneData.m_SlotSize.y;
                                }
                                else if (@float.x < 0.001f)
                                {
                                    parkingLaneData.m_SlotInterval = parkingLaneData.m_SlotSize.x;
                                    value5.m_Flags |= LaneFlags.Twoway;
                                }
                                else
                                {
                                    float2 float2 = parkingLaneData.m_SlotSize / @float.yx;
                                    float2 = math.select(float2, 0f, float2 < 0.001f);
                                    if (float2.x < float2.y)
                                    {
                                        parkingLaneData.m_SlotInterval = float2.x;
                                    }
                                    else
                                    {
                                        parkingLaneData.m_SlotInterval = float2.y;
                                        parkingLaneData.m_MaxCarLength = math.max(0f, parkingLaneData.m_SlotSize.y - 1f);
                                    }
                                }
                                value5.m_Width = math.dot(parkingLaneData.m_SlotSize, @float);
                                if (parkingLaneData.m_SlotSize.x == 0f)
                                {
                                    value5.m_Flags |= LaneFlags.Virtual;
                                }
                                nativeArray10[num9] = parkingLaneData;
                            }
                            if (flag4)
                            {
                                PedestrianLane component19 = prefab6.GetComponent<PedestrianLane>();
                                value5.m_Flags |= (LaneFlags.Pedestrian | LaneFlags.Twoway);
                                value5.m_Width = component19.m_Width;
                                if (component19.m_OnWater)
                                {
                                    value5.m_Flags |= LaneFlags.OnWater;
                                }
                            }
                            if (nativeArray14.Length != 0)
                            {
                                Entity entity2 = nativeArray[num9];
                                SecondaryLane component20 = prefab6.GetComponent<SecondaryLane>();
                                value5.m_Flags |= LaneFlags.Secondary;
                                bool flag5 = component20.m_LeftLanes != null && component20.m_LeftLanes.Length != 0;
                                bool flag6 = component20.m_RightLanes != null && component20.m_RightLanes.Length != 0;
                                bool flag7 = component20.m_CrossingLanes != null && component20.m_CrossingLanes.Length != 0;
                                SecondaryLaneData value9 = nativeArray14[num9];
                                if (component20.m_SkipSafePedestrianOverlap)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.SkipSafePedestrianOverlap;
                                }
                                if (component20.m_SkipSafeCarOverlap)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.SkipSafeCarOverlap;
                                }
                                if (component20.m_SkipUnsafeCarOverlap)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.SkipUnsafeCarOverlap;
                                }
                                if (component20.m_SkipTrackOverlap)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.SkipTrackOverlap;
                                }
                                if (component20.m_SkipMergeOverlap)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.SkipMergeOverlap;
                                }
                                if (component20.m_FitToParkingSpaces)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.FitToParkingSpaces;
                                }
                                if (component20.m_EvenSpacing)
                                {
                                    value9.m_Flags |= SecondaryLaneDataFlags.EvenSpacing;
                                }
                                value9.m_PositionOffset = component20.m_PositionOffset;
                                value9.m_LengthOffset = component20.m_LengthOffset;
                                value9.m_CutMargin = component20.m_CutMargin;
                                value9.m_CutOffset = component20.m_CutOffset;
                                value9.m_CutOverlap = component20.m_CutOverlap;
                                value9.m_Spacing = component20.m_Spacing;
                                SecondaryNetLaneFlags secondaryNetLaneFlags = (SecondaryNetLaneFlags)0;
                                if (component20.m_CanFlipSides)
                                {
                                    secondaryNetLaneFlags |= SecondaryNetLaneFlags.CanFlipSides;
                                }
                                if (component20.m_DuplicateSides)
                                {
                                    secondaryNetLaneFlags |= SecondaryNetLaneFlags.DuplicateSides;
                                }
                                if (component20.m_RequireParallel)
                                {
                                    secondaryNetLaneFlags |= SecondaryNetLaneFlags.RequireParallel;
                                }
                                if (component20.m_RequireOpposite)
                                {
                                    secondaryNetLaneFlags |= SecondaryNetLaneFlags.RequireOpposite;
                                }
                                if (flag5)
                                {
                                    SecondaryNetLaneFlags secondaryNetLaneFlags2 = secondaryNetLaneFlags | SecondaryNetLaneFlags.Left;
                                    if (!flag6)
                                    {
                                        secondaryNetLaneFlags2 |= SecondaryNetLaneFlags.OneSided;
                                    }
                                    for (int num10 = 0; num10 < component20.m_LeftLanes.Length; num10++)
                                    {
                                        SecondaryLaneInfo secondaryLaneInfo = component20.m_LeftLanes[num10];
                                        SecondaryNetLaneFlags flags = secondaryNetLaneFlags2 | secondaryLaneInfo.GetFlags();
                                        Entity entity3 = this.m_PrefabSystem.GetEntity(secondaryLaneInfo.m_Lane);
                                        base.EntityManager.GetBuffer<SecondaryNetLane>(entity3, false).Add(new SecondaryNetLane
                                        {
                                            m_Lane = entity2,
                                            m_Flags = flags
                                        });
                                    }
                                }
                                if (flag6)
                                {
                                    SecondaryNetLaneFlags secondaryNetLaneFlags3 = secondaryNetLaneFlags | SecondaryNetLaneFlags.Right;
                                    if (!flag5)
                                    {
                                        secondaryNetLaneFlags3 |= SecondaryNetLaneFlags.OneSided;
                                    }
                                    int num11 = 0;
                                IL_20E9:
                                    while (num11 < component20.m_RightLanes.Length)
                                    {
                                        SecondaryLaneInfo secondaryLaneInfo2 = component20.m_RightLanes[num11];
                                        SecondaryNetLaneFlags secondaryNetLaneFlags4 = secondaryNetLaneFlags3 | secondaryLaneInfo2.GetFlags();
                                        Entity entity4 = this.m_PrefabSystem.GetEntity(secondaryLaneInfo2.m_Lane);
                                        DynamicBuffer<SecondaryNetLane> buffer = base.EntityManager.GetBuffer<SecondaryNetLane>(entity4, false);
                                        for (int num12 = 0; num12 < buffer.Length; num12++)
                                        {
                                            SecondaryNetLane secondaryNetLane = buffer[num12];
                                            if (secondaryNetLane.m_Lane == entity2 && ((secondaryNetLane.m_Flags ^ secondaryNetLaneFlags4) & ~(SecondaryNetLaneFlags.Left | SecondaryNetLaneFlags.Right)) == (SecondaryNetLaneFlags)0)
                                            {
                                                secondaryNetLane.m_Flags |= secondaryNetLaneFlags4;
                                                buffer[num12] = secondaryNetLane;
                                                num11++;
                                                goto IL_20E9;
                                            }
                                        }
                                        buffer.Add(new SecondaryNetLane
                                        {
                                            m_Lane = entity2,
                                            m_Flags = secondaryNetLaneFlags4
                                        });
                                        num11++;
                                        goto IL_20E9;
                                    }
                                }
                                if (flag7)
                                {
                                    SecondaryNetLaneFlags secondaryNetLaneFlags5 = SecondaryNetLaneFlags.Crossing;
                                    for (int num13 = 0; num13 < component20.m_CrossingLanes.Length; num13++)
                                    {
                                        SecondaryLaneInfo2 secondaryLaneInfo3 = component20.m_CrossingLanes[num13];
                                        SecondaryNetLaneFlags flags2 = secondaryNetLaneFlags5 | secondaryLaneInfo3.GetFlags();
                                        Entity entity5 = this.m_PrefabSystem.GetEntity(secondaryLaneInfo3.m_Lane);
                                        base.EntityManager.GetBuffer<SecondaryNetLane>(entity5, false).Add(new SecondaryNetLane
                                        {
                                            m_Lane = entity2,
                                            m_Flags = flags2
                                        });
                                    }
                                }
                                nativeArray14[num9] = value9;
                            }
                            if (bufferAccessor10.Length != 0)
                            {
                                DynamicBuffer<AuxiliaryNetLane> dynamicBuffer9 = bufferAccessor10[num9];
                                AuxiliaryLanes component21 = prefab6.GetComponent<AuxiliaryLanes>();
                                if (component21 != null)
                                {
                                    dynamicBuffer9.ResizeUninitialized(component21.m_AuxiliaryLanes.Length);
                                    for (int num14 = 0; num14 < component21.m_AuxiliaryLanes.Length; num14++)
                                    {
                                        AuxiliaryLaneInfo auxiliaryLaneInfo = component21.m_AuxiliaryLanes[num14];
                                        AuxiliaryNetLane value10 = default(AuxiliaryNetLane);
                                        value10.m_Prefab = this.m_PrefabSystem.GetEntity(auxiliaryLaneInfo.m_Lane);
                                        value10.m_Position = auxiliaryLaneInfo.m_Position;
                                        value10.m_Spacing = auxiliaryLaneInfo.m_Spacing;
                                        if (auxiliaryLaneInfo.m_EvenSpacing)
                                        {
                                            value10.m_Flags |= LaneFlags.EvenSpacing;
                                        }
                                        if (auxiliaryLaneInfo.m_FindAnchor)
                                        {
                                            value10.m_Flags |= LaneFlags.FindAnchor;
                                        }
                                        NetSectionFlags netSectionFlags14;
                                        NetCompositionHelpers.GetRequirementFlags(auxiliaryLaneInfo.m_RequireAll, out value10.m_CompositionAll, out netSectionFlags14);
                                        NetSectionFlags netSectionFlags15;
                                        NetCompositionHelpers.GetRequirementFlags(auxiliaryLaneInfo.m_RequireAny, out value10.m_CompositionAny, out netSectionFlags15);
                                        NetSectionFlags netSectionFlags16;
                                        NetCompositionHelpers.GetRequirementFlags(auxiliaryLaneInfo.m_RequireNone, out value10.m_CompositionNone, out netSectionFlags16);
                                        NetSectionFlags netSectionFlags17 = netSectionFlags14 | netSectionFlags15 | netSectionFlags16;
                                        if (netSectionFlags17 != (NetSectionFlags)0)
                                        {
                                            COSystemBase.baseLog.ErrorFormat(prefab6, "Auxiliary net lane ({0}: {1}) cannot require section flags: {2}", prefab6.name, auxiliaryLaneInfo.m_Lane.name, netSectionFlags17);
                                        }
                                        dynamicBuffer9[num14] = value10;
                                        value5.m_Flags |= LaneFlags.HasAuxiliary;
                                    }
                                }
                            }
                            nativeArray9[num9] = value5;
                        }
                        NativeArray<NetLaneGeometryData> nativeArray15 = archetypeChunk.GetNativeArray<NetLaneGeometryData>(ref _Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle);
                        BufferAccessor<SubMesh> bufferAccessor11 = archetypeChunk.GetBufferAccessor<SubMesh>(ref _Game_Prefabs_SubMesh_RW_BufferTypeHandle);
                        for (int num15 = 0; num15 < nativeArray15.Length; num15++)
                        {
                            NetLaneGeometryPrefab prefab7 = this.m_PrefabSystem.GetPrefab<NetLaneGeometryPrefab>(nativeArray2[num15]);
                            NetLaneData value11 = nativeArray9[num15];
                            NetLaneGeometryData netLaneGeometryData = nativeArray15[num15];
                            DynamicBuffer<SubMesh> dynamicBuffer10 = bufferAccessor11[num15];
                            netLaneGeometryData.m_MinLod = 255;
                            netLaneGeometryData.m_GameLayers = (MeshLayer)0;
                            netLaneGeometryData.m_EditorLayers = (MeshLayer)0;
                            if (prefab7.m_Meshes != null)
                            {
                                for (int num16 = 0; num16 < prefab7.m_Meshes.Length; num16++)
                                {
                                    NetLaneMeshInfo netLaneMeshInfo = prefab7.m_Meshes[num16];
                                    RenderPrefab mesh = netLaneMeshInfo.m_Mesh;
                                    Entity entity6 = this.m_PrefabSystem.GetEntity(mesh);
                                    MeshData componentData = base.EntityManager.GetComponentData<MeshData>(entity6);
                                    float3 y = MathUtils.Size(mesh.bounds);
                                    componentData.m_MinLod = (byte)RenderingUtils.CalculateLodLimit(RenderingUtils.GetRenderingSize(y.xy), componentData.m_LodBias);
                                    componentData.m_ShadowLod = (byte)RenderingUtils.CalculateLodLimit(RenderingUtils.GetShadowRenderingSize(y.xy), componentData.m_ShadowBias);
                                    netLaneGeometryData.m_Size = math.max(netLaneGeometryData.m_Size, y);
                                    netLaneGeometryData.m_MinLod = math.min(netLaneGeometryData.m_MinLod, (int)componentData.m_MinLod);
                                    SubMeshFlags subMeshFlags = (SubMeshFlags)0U;
                                    if (netLaneMeshInfo.m_RequireSafe)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireSafe;
                                    }
                                    if (netLaneMeshInfo.m_RequireLevelCrossing)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireLevelCrossing;
                                    }
                                    if (netLaneMeshInfo.m_RequireEditor)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireEditor;
                                    }
                                    if (netLaneMeshInfo.m_RequireTrackCrossing)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireTrack;
                                    }
                                    if (netLaneMeshInfo.m_RequireClear)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireClear;
                                    }
                                    if (netLaneMeshInfo.m_RequireLeftHandTraffic)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireLeftHandTraffic;
                                    }
                                    if (netLaneMeshInfo.m_RequireRightHandTraffic)
                                    {
                                        subMeshFlags |= SubMeshFlags.RequireRightHandTraffic;
                                    }
                                    dynamicBuffer10.Add(new SubMesh(entity6, subMeshFlags, (ushort)num16));
                                    MeshLayer meshLayer = (componentData.m_DefaultLayers == (MeshLayer)0) ? MeshLayer.Default : componentData.m_DefaultLayers;
                                    if (!netLaneMeshInfo.m_RequireEditor)
                                    {
                                        netLaneGeometryData.m_GameLayers |= meshLayer;
                                    }
                                    netLaneGeometryData.m_EditorLayers |= meshLayer;
                                    base.EntityManager.SetComponentData<MeshData>(entity6, componentData);
                                    if (mesh.Has<ColorProperties>())
                                    {
                                        value11.m_Flags |= LaneFlags.PseudoRandom;
                                    }
                                }
                            }
                            nativeArray9[num15] = value11;
                            nativeArray15[num15] = netLaneGeometryData;
                        }
                        NativeArray<SpawnableObjectData> nativeArray16 = archetypeChunk.GetNativeArray<SpawnableObjectData>(ref _Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle);
                        if (nativeArray16.Length != 0)
                        {
                            for (int num17 = 0; num17 < nativeArray16.Length; num17++)
                            {
                                Entity obj = nativeArray[num17];
                                SpawnableObjectData value12 = nativeArray16[num17];
                                SpawnableLane component22 = this.m_PrefabSystem.GetPrefab<NetLanePrefab>(nativeArray2[num17]).GetComponent<SpawnableLane>();
                                for (int num18 = 0; num18 < component22.m_Placeholders.Length; num18++)
                                {
                                    NetLanePrefab prefab8 = component22.m_Placeholders[num18];
                                    Entity entity7 = this.m_PrefabSystem.GetEntity(prefab8);
                                    base.EntityManager.GetBuffer<PlaceholderObjectElement>(entity7, false).Add(new PlaceholderObjectElement(obj));
                                }
                                if (component22.m_RandomizationGroup != null)
                                {
                                    value12.m_RandomizationGroup = this.m_PrefabSystem.GetEntity(component22.m_RandomizationGroup);
                                }
                                value12.m_Probability = component22.m_Probability;
                                nativeArray16[num17] = value12;
                            }
                        }
                    }
                    NativeArray<RoadData> nativeArray17 = archetypeChunk.GetNativeArray<RoadData>(ref _Game_Prefabs_RoadData_RW_ComponentTypeHandle);
                    if (nativeArray17.Length != 0)
                    {
                        for (int num19 = 0; num19 < nativeArray17.Length; num19++)
                        {
                            RoadPrefab prefab9 = this.m_PrefabSystem.GetPrefab<RoadPrefab>(nativeArray2[num19]);
                            NetData value13 = nativeArray3[num19];
                            NetGeometryData netGeometryData = nativeArray4[num19];
                            RoadData value14 = nativeArray17[num19];
                            RoadType roadType = prefab9.m_RoadType;
                            if (roadType != RoadType.Normal)
                            {
                                if (roadType == RoadType.PublicTransport)
                                {
                                    value13.m_RequiredLayers |= Layer.PublicTransportRoad;
                                }
                            }
                            else
                            {
                                value13.m_RequiredLayers |= Layer.Road;
                            }
                            value13.m_ConnectLayers |= (Layer.Road | Layer.TrainTrack | Layer.Pathway | Layer.TramTrack | Layer.Fence | Layer.PublicTransportRoad);
                            value13.m_LocalConnectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            value13.m_NodePriority += 2000f;
                            netGeometryData.m_MergeLayers |= (Layer.Road | Layer.TramTrack | Layer.PublicTransportRoad);
                            netGeometryData.m_IntersectLayers |= (Layer.Road | Layer.TrainTrack | Layer.Pathway | Layer.TramTrack | Layer.PublicTransportRoad);
                            netGeometryData.m_Flags |= (GeometryFlags.SupportRoundabout | GeometryFlags.BlockZone | GeometryFlags.Directional | GeometryFlags.FlattenTerrain | GeometryFlags.ClipTerrain);
                            value14.m_SpeedLimit = prefab9.m_SpeedLimit / 3.6f;
                            if (prefab9.m_ZoneBlock != null)
                            {
                                netGeometryData.m_Flags |= GeometryFlags.SnapCellSize;
                                value14.m_ZoneBlockPrefab = this.m_PrefabSystem.GetEntity(prefab9.m_ZoneBlock);
                                value14.m_Flags |= RoadFlags.EnableZoning;
                            }
                            if (prefab9.m_TrafficLights)
                            {
                                value14.m_Flags |= RoadFlags.PreferTrafficLights;
                            }
                            if (prefab9.m_HighwayRules)
                            {
                                value14.m_Flags |= RoadFlags.UseHighwayRules;
                                netGeometryData.m_MinNodeOffset = math.max(netGeometryData.m_MinNodeOffset, 2f);
                                netGeometryData.m_Flags |= GeometryFlags.SmoothElevation;
                            }
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value15 = nativeArray5[num19];
                                value15.m_PlacementFlags |= PlacementFlags.OnGround;
                                nativeArray5[num19] = value15;
                            }
                            nativeArray3[num19] = value13;
                            nativeArray4[num19] = netGeometryData;
                            nativeArray17[num19] = value14;
                        }
                    }
                    NativeArray<TrackData> nativeArray18 = archetypeChunk.GetNativeArray<TrackData>(ref _Game_Prefabs_TrackData_RW_ComponentTypeHandle);
                    if (nativeArray18.Length != 0)
                    {
                        int num20 = 0;
                        while (num20 < nativeArray18.Length)
                        {
                            TrackPrefab prefab10 = this.m_PrefabSystem.GetPrefab<TrackPrefab>(nativeArray2[num20]);
                            NetData value16 = nativeArray3[num20];
                            NetGeometryData netGeometryData2 = nativeArray4[num20];
                            TrackData value17 = nativeArray18[num20];
                            Layer layer2;
                            Layer layer3;
                            float num21;
                            float y2;
                            switch (prefab10.m_TrackType)
                            {
                                case TrackTypes.Train:
                                    layer2 = Layer.TrainTrack;
                                    layer3 = (Layer.TrainTrack | Layer.Pathway);
                                    num21 = 200f;
                                    y2 = 10f;
                                    netGeometryData2.m_Flags |= GeometryFlags.SmoothElevation;
                                    break;
                                case TrackTypes.Tram:
                                    layer2 = Layer.TramTrack;
                                    layer3 = Layer.TramTrack;
                                    num21 = 0f;
                                    y2 = 8f;
                                    netGeometryData2.m_Flags |= GeometryFlags.SupportRoundabout;
                                    break;
                                case TrackTypes.Train | TrackTypes.Tram:
                                    goto IL_2979;
                                case TrackTypes.Subway:
                                    layer2 = Layer.SubwayTrack;
                                    layer3 = Layer.SubwayTrack;
                                    num21 = 200f;
                                    y2 = 9f;
                                    netGeometryData2.m_Flags |= GeometryFlags.SmoothElevation;
                                    break;
                                default:
                                    goto IL_2979;
                            }
                        IL_2991:
                            value16.m_RequiredLayers |= layer2;
                            value16.m_ConnectLayers |= layer3;
                            value16.m_LocalConnectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            netGeometryData2.m_MergeLayers |= layer2;
                            netGeometryData2.m_IntersectLayers |= layer3;
                            netGeometryData2.m_EdgeLengthRange.max = math.max(netGeometryData2.m_EdgeLengthRange.max, num21 * 1.5f);
                            netGeometryData2.m_MinNodeOffset = math.max(netGeometryData2.m_MinNodeOffset, y2);
                            netGeometryData2.m_Flags |= (GeometryFlags.BlockZone | GeometryFlags.Directional | GeometryFlags.FlattenTerrain | GeometryFlags.ClipTerrain);
                            value17.m_TrackType = prefab10.m_TrackType;
                            value17.m_SpeedLimit = prefab10.m_SpeedLimit / 3.6f;
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value18 = nativeArray5[num20];
                                value18.m_PlacementFlags |= PlacementFlags.OnGround;
                                nativeArray5[num20] = value18;
                            }
                            nativeArray3[num20] = value16;
                            nativeArray4[num20] = netGeometryData2;
                            nativeArray18[num20] = value17;
                            num20++;
                            continue;
                        IL_2979:
                            layer2 = Layer.None;
                            layer3 = Layer.None;
                            num21 = 0f;
                            y2 = 0f;
                            goto IL_2991;
                        }
                    }
                    NativeArray<WaterwayData> nativeArray19 = archetypeChunk.GetNativeArray<WaterwayData>(ref _Game_Prefabs_WaterwayData_RW_ComponentTypeHandle);
                    if (nativeArray19.Length != 0)
                    {
                        for (int num22 = 0; num22 < nativeArray19.Length; num22++)
                        {
                            WaterwayPrefab prefab11 = this.m_PrefabSystem.GetPrefab<WaterwayPrefab>(nativeArray2[num22]);
                            NetData value19 = nativeArray3[num22];
                            NetGeometryData value20 = nativeArray4[num22];
                            WaterwayData value21 = nativeArray19[num22];
                            value19.m_RequiredLayers |= Layer.Waterway;
                            value19.m_ConnectLayers |= Layer.Waterway;
                            value19.m_LocalConnectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            value20.m_MergeLayers |= Layer.Waterway;
                            value20.m_IntersectLayers |= Layer.Waterway;
                            value20.m_EdgeLengthRange.max = 1000f;
                            value20.m_ElevatedLength = 1000f;
                            value20.m_Flags |= (GeometryFlags.BlockZone | GeometryFlags.Directional | GeometryFlags.FlattenTerrain | GeometryFlags.OnWater);
                            value21.m_SpeedLimit = prefab11.m_SpeedLimit / 3.6f;
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value22 = nativeArray5[num22];
                                value22.m_PlacementFlags |= PlacementFlags.Floating;
                                value22.m_SnapDistance = 16f;
                                nativeArray5[num22] = value22;
                            }
                            nativeArray3[num22] = value19;
                            nativeArray4[num22] = value20;
                            nativeArray19[num22] = value21;
                        }
                    }
                    NativeArray<PathwayData> nativeArray20 = archetypeChunk.GetNativeArray<PathwayData>(ref _Game_Prefabs_PathwayData_RW_ComponentTypeHandle);
                    if (nativeArray20.Length != 0)
                    {
                        NativeArray<LocalConnectData> nativeArray21 = archetypeChunk.GetNativeArray<LocalConnectData>(ref _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle);
                        for (int num23 = 0; num23 < nativeArray20.Length; num23++)
                        {
                            PathwayPrefab prefab12 = this.m_PrefabSystem.GetPrefab<PathwayPrefab>(nativeArray2[num23]);
                            NetData value23 = nativeArray3[num23];
                            NetGeometryData netGeometryData3 = nativeArray4[num23];
                            LocalConnectData value24 = nativeArray21[num23];
                            PathwayData value25 = nativeArray20[num23];
                            Layer layer4 = flag ? Layer.MarkerPathway : Layer.Pathway;
                            value23.m_RequiredLayers |= layer4;
                            value23.m_ConnectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            value23.m_LocalConnectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            netGeometryData3.m_MergeLayers |= layer4;
                            netGeometryData3.m_IntersectLayers |= (Layer.Pathway | Layer.MarkerPathway);
                            netGeometryData3.m_ElevationLimit = 2f;
                            netGeometryData3.m_Flags |= GeometryFlags.Directional;
                            if (flag)
                            {
                                netGeometryData3.m_ElevatedLength = math.max(MaxPillarInterval, netGeometryData3.m_EdgeLengthRange.max);
                                logger.LogMessage("Max pillar interval changed from " + netGeometryData3.m_EdgeLengthRange.max + " to " + math.max(MaxPillarInterval, netGeometryData3.m_EdgeLengthRange.max));
                                netGeometryData3.m_Flags |= (GeometryFlags.LoweredIsTunnel | GeometryFlags.RaisedIsElevated);
                            }
                            else
                            {
                                netGeometryData3.m_ElevatedLength = MaxPillarInterval;
                                logger.LogMessage("Max pillar interval changed from 40 to " + MaxPillarInterval);
                                netGeometryData3.m_Flags |= (GeometryFlags.BlockZone | GeometryFlags.FlattenTerrain | GeometryFlags.ClipTerrain);
                            }
                            value24.m_Flags |= (LocalConnectFlags.KeepOpen | LocalConnectFlags.RequireDeadend | LocalConnectFlags.ChooseBest | LocalConnectFlags.ChooseSides);
                            value24.m_Layers |= (Layer.Road | Layer.TrainTrack | Layer.Pathway | Layer.Waterway | Layer.TramTrack | Layer.SubwayTrack | Layer.MarkerPathway | Layer.PublicTransportRoad);
                            value24.m_HeightRange = new Bounds1(-8f, 8f);
                            value24.m_SearchDistance = 4f;
                            value25.m_SpeedLimit = prefab12.m_SpeedLimit / 3.6f;
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value26 = nativeArray5[num23];
                                value26.m_PlacementFlags |= PlacementFlags.OnGround;
                                value26.m_SnapDistance = (flag ? 2f : 4f);
                                nativeArray5[num23] = value26;
                            }
                            nativeArray3[num23] = value23;
                            nativeArray4[num23] = netGeometryData3;
                            nativeArray21[num23] = value24;
                            nativeArray20[num23] = value25;
                        }
                    }
                    NativeArray<TaxiwayData> nativeArray22 = archetypeChunk.GetNativeArray<TaxiwayData>(ref _Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle);
                    if (nativeArray22.Length != 0)
                    {
                        for (int num24 = 0; num24 < nativeArray22.Length; num24++)
                        {
                            TaxiwayPrefab prefab13 = this.m_PrefabSystem.GetPrefab<TaxiwayPrefab>(nativeArray2[num24]);
                            NetData value27 = nativeArray3[num24];
                            NetGeometryData value28 = nativeArray4[num24];
                            TaxiwayData value29 = nativeArray22[num24];
                            Layer layer5 = flag ? Layer.MarkerTaxiway : Layer.Taxiway;
                            value27.m_RequiredLayers |= layer5;
                            value27.m_ConnectLayers |= (Layer.Pathway | Layer.Taxiway | Layer.MarkerPathway | Layer.MarkerTaxiway);
                            value28.m_MergeLayers |= layer5;
                            value28.m_IntersectLayers |= (Layer.Pathway | Layer.Taxiway | Layer.MarkerPathway | Layer.MarkerTaxiway);
                            value28.m_EdgeLengthRange.max = 1000f;
                            value28.m_ElevatedLength = 1000f;
                            value28.m_Flags |= GeometryFlags.Directional;
                            if (!flag)
                            {
                                value28.m_Flags |= (GeometryFlags.BlockZone | GeometryFlags.FlattenTerrain | GeometryFlags.ClipTerrain);
                            }
                            value29.m_SpeedLimit = prefab13.m_SpeedLimit / 3.6f;
                            if (prefab13.m_Airspace)
                            {
                                if (prefab13.m_Runway)
                                {
                                    value29.m_Flags |= TaxiwayFlags.Runway;
                                }
                                else if (!prefab13.m_Taxiway)
                                {
                                    value28.m_Flags |= (GeometryFlags.RaisedIsElevated | GeometryFlags.BlockZone | GeometryFlags.FlattenTerrain);
                                }
                                value29.m_Flags |= TaxiwayFlags.Airspace;
                            }
                            else if (prefab13.m_Runway)
                            {
                                value29.m_Flags |= TaxiwayFlags.Runway;
                            }
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value30 = nativeArray5[num24];
                                value30.m_PlacementFlags |= PlacementFlags.OnGround;
                                value30.m_SnapDistance = (flag ? 4f : 8f);
                                nativeArray5[num24] = value30;
                            }
                            nativeArray3[num24] = value27;
                            nativeArray4[num24] = value28;
                            nativeArray22[num24] = value29;
                        }
                    }
                    bool flag8 = archetypeChunk.Has<PowerLineData>(ref _Game_Prefabs_PowerLineData_RO_ComponentTypeHandle);
                    if (flag8)
                    {
                        for (int num25 = 0; num25 < nativeArray.Length; num25++)
                        {
                            PowerLinePrefab prefab14 = this.m_PrefabSystem.GetPrefab<PowerLinePrefab>(nativeArray2[num25]);
                            NetGeometryData value31 = nativeArray4[num25];
                            bool flag9 = false;
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData placeableNetData2 = nativeArray5[num25];
                                placeableNetData2.m_PlacementFlags |= PlacementFlags.OnGround;
                                flag9 = (placeableNetData2.m_ElevationRange.max < 0f);
                                nativeArray5[num25] = placeableNetData2;
                            }
                            value31.m_EdgeLengthRange.max = prefab14.m_MaxPylonDistance;
                            value31.m_ElevatedLength = prefab14.m_MaxPylonDistance;
                            value31.m_Hanging = prefab14.m_Hanging;
                            value31.m_Flags |= (GeometryFlags.StrictNodes | GeometryFlags.LoweredIsTunnel | GeometryFlags.RaisedIsElevated);
                            if (!flag)
                            {
                                value31.m_Flags |= GeometryFlags.FlattenTerrain;
                            }
                            if (flag9)
                            {
                                value31.m_IntersectLayers |= (Layer.PowerlineLow | Layer.PowerlineHigh);
                                value31.m_MergeLayers |= (Layer.PowerlineLow | Layer.PowerlineHigh);
                            }
                            else
                            {
                                value31.m_Flags |= (GeometryFlags.StraightEdges | GeometryFlags.NoEdgeConnection | GeometryFlags.SnapToNetAreas | GeometryFlags.BlockZone | GeometryFlags.StandingNodes);
                            }
                            nativeArray4[num25] = value31;
                        }
                    }
                    bool flag10 = archetypeChunk.Has<PipelineData>(ref _Game_Prefabs_PipelineData_RO_ComponentTypeHandle);
                    if (flag10)
                    {
                        for (int num26 = 0; num26 < nativeArray.Length; num26++)
                        {
                            this.m_PrefabSystem.GetPrefab<PipelinePrefab>(nativeArray2[num26]);
                            NetGeometryData netGeometryData4 = nativeArray4[num26];
                            netGeometryData4.m_ElevatedLength = netGeometryData4.m_EdgeLengthRange.max;
                            netGeometryData4.m_Flags |= (GeometryFlags.StrictNodes | GeometryFlags.LoweredIsTunnel | GeometryFlags.RaisedIsElevated);
                            netGeometryData4.m_IntersectLayers |= (Layer.WaterPipe | Layer.SewagePipe | Layer.StormwaterPipe);
                            netGeometryData4.m_MergeLayers |= (Layer.WaterPipe | Layer.SewagePipe | Layer.StormwaterPipe);
                            if (!flag)
                            {
                                netGeometryData4.m_Flags |= GeometryFlags.FlattenTerrain;
                            }
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value32 = nativeArray5[num26];
                                value32.m_PlacementFlags |= PlacementFlags.OnGround;
                                nativeArray5[num26] = value32;
                            }
                            nativeArray4[num26] = netGeometryData4;
                        }
                    }
                    if (archetypeChunk.Has<FenceData>(ref _Game_Prefabs_FenceData_RO_ComponentTypeHandle))
                    {
                        for (int num27 = 0; num27 < nativeArray.Length; num27++)
                        {
                            this.m_PrefabSystem.GetPrefab<FencePrefab>(nativeArray2[num27]);
                            NetData value33 = nativeArray3[num27];
                            NetGeometryData netGeometryData5 = nativeArray4[num27];
                            value33.m_RequiredLayers |= Layer.Fence;
                            value33.m_ConnectLayers |= Layer.Fence;
                            netGeometryData5.m_ElevatedLength = netGeometryData5.m_EdgeLengthRange.max;
                            netGeometryData5.m_Flags |= (GeometryFlags.StrictNodes | GeometryFlags.BlockZone | GeometryFlags.FlattenTerrain);
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value34 = nativeArray5[num27];
                                value34.m_PlacementFlags |= PlacementFlags.OnGround;
                                value34.m_SnapDistance = 4f;
                                nativeArray5[num27] = value34;
                            }
                            nativeArray3[num27] = value33;
                            nativeArray4[num27] = netGeometryData5;
                        }
                    }
                    if (archetypeChunk.Has<EditorContainerData>(ref _Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle))
                    {
                        for (int num28 = 0; num28 < nativeArray3.Length; num28++)
                        {
                            NetData value35 = nativeArray3[num28];
                            value35.m_RequiredLayers |= Layer.LaneEditor;
                            value35.m_ConnectLayers |= Layer.LaneEditor;
                            nativeArray3[num28] = value35;
                        }
                    }
                    if (flag2)
                    {
                        BufferAccessor<FixedNetElement> bufferAccessor12 = archetypeChunk.GetBufferAccessor<FixedNetElement>(ref _Game_Prefabs_FixedNetElement_RW_BufferTypeHandle);
                        for (int num29 = 0; num29 < nativeArray4.Length; num29++)
                        {
                            NetGeometryPrefab prefab15 = this.m_PrefabSystem.GetPrefab<NetGeometryPrefab>(nativeArray2[num29]);
                            Bridge component23 = prefab15.GetComponent<Bridge>();
                            NetData value36 = nativeArray3[num29];
                            NetGeometryData netGeometryData6 = nativeArray4[num29];
                            value36.m_NodePriority += 1000f;
                            if (component23.m_SegmentLength > 0.1f)
                            {
                                netGeometryData6.m_EdgeLengthRange.min = component23.m_SegmentLength * 0.6f;
                                netGeometryData6.m_EdgeLengthRange.max = math.max(component23.m_SegmentLength * 1.4f, (float)MaxElevatedLength);
                                logger.LogMessage("Max node interval changed from" + component23.m_SegmentLength * 1.4f + " to " + math.max(component23.m_SegmentLength * 1.4f, (float)MaxElevatedLength));
                            }
                            netGeometryData6.m_ElevatedLength = netGeometryData6.m_EdgeLengthRange.max;
                            netGeometryData6.m_Hanging = component23.m_Hanging;
                            netGeometryData6.m_Flags |= (GeometryFlags.StraightEdges | GeometryFlags.StraightEnds | GeometryFlags.RequireElevated | GeometryFlags.SymmetricalEdges | GeometryFlags.SmoothSlopes);
                            if (nativeArray5.Length != 0)
                            {
                                PlaceableNetData value37 = nativeArray5[num29];
                                BridgeWaterFlow waterFlow = component23.m_WaterFlow;
                                if (waterFlow != BridgeWaterFlow.Left)
                                {
                                    if (waterFlow == BridgeWaterFlow.Right)
                                    {
                                        value37.m_PlacementFlags |= PlacementFlags.FlowRight;
                                    }
                                }
                                else
                                {
                                    value37.m_PlacementFlags |= PlacementFlags.FlowLeft;
                                }
                                nativeArray5[num29] = value37;
                            }
                            if (bufferAccessor12.Length != 0)
                            {
                                DynamicBuffer<FixedNetElement> dynamicBuffer11 = bufferAccessor12[num29];
                                dynamicBuffer11.ResizeUninitialized(component23.m_FixedSegments.Length);
                                int num30 = 0;
                                bool flag11 = false;
                                for (int num31 = 0; num31 < dynamicBuffer11.Length; num31++)
                                {
                                    FixedNetSegmentInfo fixedNetSegmentInfo = component23.m_FixedSegments[num31];
                                    flag11 |= (fixedNetSegmentInfo.m_Length <= 0.1f);
                                }
                                for (int num32 = 0; num32 < dynamicBuffer11.Length; num32++)
                                {
                                    FixedNetSegmentInfo fixedNetSegmentInfo2 = component23.m_FixedSegments[num32];
                                    FixedNetElement value38;
                                    value38.m_Flags = (FixedNetFlags)0U;
                                    if (fixedNetSegmentInfo2.m_Length > 0.1f)
                                    {
                                        if (flag11)
                                        {
                                            value38.m_LengthRange.min = fixedNetSegmentInfo2.m_Length;
                                            value38.m_LengthRange.max = fixedNetSegmentInfo2.m_Length;
                                        }
                                        else
                                        {
                                            value38.m_LengthRange.min = fixedNetSegmentInfo2.m_Length * 0.6f;
                                            value38.m_LengthRange.max = fixedNetSegmentInfo2.m_Length * 1.4f;
                                        }
                                    }
                                    else
                                    {
                                        value38.m_LengthRange = netGeometryData6.m_EdgeLengthRange;
                                    }
                                    if (fixedNetSegmentInfo2.m_CanCurve)
                                    {
                                        netGeometryData6.m_Flags &= ~GeometryFlags.StraightEdges;
                                        num30++;
                                    }
                                    else
                                    {
                                        value38.m_Flags |= FixedNetFlags.Straight;
                                    }
                                    value38.m_CountRange = fixedNetSegmentInfo2.m_CountRange;
                                    NetSectionFlags netSectionFlags18;
                                    NetCompositionHelpers.GetRequirementFlags(fixedNetSegmentInfo2.m_SetState, out value38.m_SetState, out netSectionFlags18);
                                    NetSectionFlags netSectionFlags19;
                                    NetCompositionHelpers.GetRequirementFlags(fixedNetSegmentInfo2.m_UnsetState, out value38.m_UnsetState, out netSectionFlags19);
                                    if ((netSectionFlags18 | netSectionFlags19) != (NetSectionFlags)0)
                                    {
                                        COSystemBase.baseLog.ErrorFormat(prefab15, "Net segment state ({0}) cannot (un)set section flags: {1}", prefab15.name, netSectionFlags18 | netSectionFlags19);
                                    }
                                    dynamicBuffer11[num32] = value38;
                                }
                                if (num30 >= 2)
                                {
                                    netGeometryData6.m_Flags |= GeometryFlags.NoCurveSplit;
                                }
                            }
                            nativeArray3[num29] = value36;
                            nativeArray4[num29] = netGeometryData6;
                        }
                    }
                    NativeArray<ElectricityConnectionData> nativeArray23 = archetypeChunk.GetNativeArray<ElectricityConnectionData>(ref _Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle);
                    if (nativeArray23.Length != 0)
                    {
                        NativeArray<LocalConnectData> nativeArray24 = archetypeChunk.GetNativeArray<LocalConnectData>(ref _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle);
                        for (int num33 = 0; num33 < nativeArray23.Length; num33++)
                        {
                            NetPrefab prefab16 = this.m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[num33]);
                            ElectricityConnection component24 = prefab16.GetComponent<ElectricityConnection>();
                            NetData value39 = nativeArray3[num33];
                            ElectricityConnectionData value40 = nativeArray23[num33];
                            ElectricityConnection.Voltage voltage = component24.m_Voltage;
                            Layer layer6;
                            Layer layer7;
                            float snapDistance;
                            if (voltage != ElectricityConnection.Voltage.Low)
                            {
                                if (voltage != ElectricityConnection.Voltage.High)
                                {
                                    layer6 = Layer.None;
                                    layer7 = Layer.None;
                                    snapDistance = 8f;
                                }
                                else
                                {
                                    layer6 = Layer.PowerlineHigh;
                                    layer7 = Layer.PowerlineHigh;
                                    snapDistance = 8f;
                                }
                            }
                            else
                            {
                                layer6 = Layer.PowerlineLow;
                                layer7 = (Layer.Road | Layer.PowerlineLow);
                                snapDistance = 4f;
                            }
                            if (flag8)
                            {
                                value39.m_RequiredLayers |= layer6;
                                value39.m_ConnectLayers |= layer6;
                                LocalConnectData value41 = nativeArray24[num33];
                                value41.m_Flags |= (LocalConnectFlags.ExplicitNodes | LocalConnectFlags.ChooseBest);
                                value41.m_Layers |= layer7;
                                value41.m_HeightRange = new Bounds1(-100f, 100f);
                                value41.m_SearchDistance = 0f;
                                if (flag)
                                {
                                    value41.m_Flags |= LocalConnectFlags.KeepOpen;
                                    value41.m_SearchDistance = 4f;
                                }
                                nativeArray24[num33] = value41;
                                if (nativeArray5.Length != 0)
                                {
                                    PlaceableNetData value42 = nativeArray5[num33];
                                    value42.m_SnapDistance = snapDistance;
                                    nativeArray5[num33] = value42;
                                }
                            }
                            value39.m_LocalConnectLayers |= layer6;
                            value40.m_Direction = component24.m_Direction;
                            value40.m_Capacity = component24.m_Capacity;
                            value40.m_Voltage = component24.m_Voltage;
                            NetSectionFlags netSectionFlags20;
                            NetCompositionHelpers.GetRequirementFlags(component24.m_RequireAll, out value40.m_CompositionAll, out netSectionFlags20);
                            NetSectionFlags netSectionFlags21;
                            NetCompositionHelpers.GetRequirementFlags(component24.m_RequireAny, out value40.m_CompositionAny, out netSectionFlags21);
                            NetSectionFlags netSectionFlags22;
                            NetCompositionHelpers.GetRequirementFlags(component24.m_RequireNone, out value40.m_CompositionNone, out netSectionFlags22);
                            NetSectionFlags netSectionFlags23 = netSectionFlags20 | netSectionFlags21 | netSectionFlags22;
                            if (netSectionFlags23 != (NetSectionFlags)0)
                            {
                                COSystemBase.baseLog.ErrorFormat(prefab16, "Electricity connection ({0}) cannot require section flags: {1}", prefab16.name, netSectionFlags23);
                            }
                            nativeArray3[num33] = value39;
                            nativeArray23[num33] = value40;
                        }
                    }
                    NativeArray<WaterPipeConnectionData> nativeArray25 = archetypeChunk.GetNativeArray<WaterPipeConnectionData>(ref _Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle);
                    if (nativeArray25.Length != 0)
                    {
                        NativeArray<LocalConnectData> nativeArray26 = archetypeChunk.GetNativeArray<LocalConnectData>(ref _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle);
                        for (int num34 = 0; num34 < nativeArray25.Length; num34++)
                        {
                            WaterPipeConnection component25 = this.m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[num34]).GetComponent<WaterPipeConnection>();
                            NetData value43 = nativeArray3[num34];
                            WaterPipeConnectionData value44 = nativeArray25[num34];
                            Layer layer8 = Layer.None;
                            if (component25.m_FreshCapacity != 0)
                            {
                                layer8 |= Layer.WaterPipe;
                            }
                            if (component25.m_SewageCapacity != 0)
                            {
                                layer8 |= Layer.SewagePipe;
                            }
                            if (component25.m_StormCapacity != 0)
                            {
                                layer8 |= Layer.StormwaterPipe;
                            }
                            if (flag10)
                            {
                                value43.m_RequiredLayers |= layer8;
                                value43.m_ConnectLayers |= layer8;
                                LocalConnectData value45 = nativeArray26[num34];
                                value45.m_Flags |= (LocalConnectFlags.ExplicitNodes | LocalConnectFlags.ChooseBest);
                                value45.m_Layers |= (Layer.Road | layer8);
                                value45.m_HeightRange = new Bounds1(-100f, 100f);
                                value45.m_SearchDistance = 0f;
                                if (flag)
                                {
                                    value45.m_Flags |= LocalConnectFlags.KeepOpen;
                                    value45.m_SearchDistance = 4f;
                                }
                                nativeArray26[num34] = value45;
                                if (nativeArray5.Length != 0)
                                {
                                    PlaceableNetData value46 = nativeArray5[num34];
                                    value46.m_SnapDistance = 4f;
                                    nativeArray5[num34] = value46;
                                }
                            }
                            value43.m_LocalConnectLayers |= layer8;
                            value44.m_FreshCapacity = component25.m_FreshCapacity;
                            value44.m_SewageCapacity = component25.m_SewageCapacity;
                            value44.m_StormCapacity = component25.m_StormCapacity;
                            nativeArray3[num34] = value43;
                            nativeArray25[num34] = value44;
                        }
                    }
                }
            }
            catch
            {
                chunks.Dispose();
                throw;
            }
            this.m_PathfindHeuristicData.value = new PathfindHeuristicData
            {
                m_CarCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f),
                m_TrackCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f),
                m_PedestrianCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f),
                m_FlyingCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f),
                m_TaxiCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f),
                m_OffRoadCosts = new PathfindCosts(1000000f, 1000000f, 1000000f, 1000000f)
            };
            this.__TypeHandle.__Game_Prefabs_NetPieceObject_RO_BufferLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetPieceLane_RO_BufferLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetSectionPiece_RO_BufferLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetSubSection_RO_BufferLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetVertexMatchData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetPieceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
            PatchedNetInitializeSystem.InitializeNetDefaultsJob initializeNetDefaultsJob = default(PatchedNetInitializeSystem.InitializeNetDefaultsJob);
            initializeNetDefaultsJob.m_Chunks = chunks;
            initializeNetDefaultsJob.m_NetGeometrySectionType = this.__TypeHandle.__Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle;
            initializeNetDefaultsJob.m_NetType = this.__TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_NetGeometryType = this.__TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_PlaceableNetType = this.__TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_RoadType = this.__TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_DefaultNetLaneType = this.__TypeHandle.__Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle;
            initializeNetDefaultsJob.m_NetPieceData = this.__TypeHandle.__Game_Prefabs_NetPieceData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetLaneData = this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetVertexMatchData = this.__TypeHandle.__Game_Prefabs_NetVertexMatchData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_PlaceableNetPieceData = this.__TypeHandle.__Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_PlaceableObjectData = this.__TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetSubSectionData = this.__TypeHandle.__Game_Prefabs_NetSubSection_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetSectionPieceData = this.__TypeHandle.__Game_Prefabs_NetSectionPiece_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetPieceLanes = this.__TypeHandle.__Game_Prefabs_NetPieceLane_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetPieceObjects = this.__TypeHandle.__Game_Prefabs_NetPieceObject_RO_BufferLookup;
            PatchedNetInitializeSystem.InitializeNetDefaultsJob jobData = initializeNetDefaultsJob;
            this.__TypeHandle.__Game_Prefabs_PathfindConnectionData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PathfindTransportData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PathfindTrackData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PathfindCarData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            PatchedNetInitializeSystem.CollectPathfindDataJob collectPathfindDataJob = default(PatchedNetInitializeSystem.CollectPathfindDataJob);
            collectPathfindDataJob.m_NetLaneDataType = this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentTypeHandle;
            collectPathfindDataJob.m_ConnectionLaneDataType = this.__TypeHandle.__Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle;
            collectPathfindDataJob.m_PathfindCarData = this.__TypeHandle.__Game_Prefabs_PathfindCarData_RO_ComponentLookup;
            collectPathfindDataJob.m_PathfindTrackData = this.__TypeHandle.__Game_Prefabs_PathfindTrackData_RO_ComponentLookup;
            collectPathfindDataJob.m_PathfindPedestrianData = this.__TypeHandle.__Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup;
            collectPathfindDataJob.m_PathfindTransportData = this.__TypeHandle.__Game_Prefabs_PathfindTransportData_RO_ComponentLookup;
            collectPathfindDataJob.m_PathfindConnectionData = this.__TypeHandle.__Game_Prefabs_PathfindConnectionData_RO_ComponentLookup;
            collectPathfindDataJob.m_PathfindHeuristicData = this.m_PathfindHeuristicData;
            PatchedNetInitializeSystem.CollectPathfindDataJob jobData2 = collectPathfindDataJob;
            JobHandle job = jobData.Schedule(chunks.Length, 1, base.Dependency);
            JobHandle jobHandle = jobData2.Schedule(this.m_LaneQuery, base.Dependency);
            this.m_PathfindHeuristicDeps = jobHandle;
            base.Dependency = JobHandle.CombineDependencies(job, jobHandle);
        }

        // Token: 0x060069FB RID: 27131 RVA: 0x0005E08F File Offset: 0x0005C28F
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
        }

        // Token: 0x060069FC RID: 27132 RVA: 0x00099B0E File Offset: 0x00097D0E
        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            this.__AssignQueries(ref base.CheckedStateRef);
            this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        // Token: 0x060069FD RID: 27133 RVA: 0x0005E948 File Offset: 0x0005CB48
        [Preserve]
        public PatchedNetInitializeSystem()
        {
        }

        // Token: 0x0400C08E RID: 49294
        private PrefabSystem m_PrefabSystem;

        // Token: 0x0400C08F RID: 49295
        private EntityQuery m_PrefabQuery;

        // Token: 0x0400C090 RID: 49296
        private EntityQuery m_LaneQuery;

        // Token: 0x0400C091 RID: 49297
        private NativeValue<PathfindHeuristicData> m_PathfindHeuristicData;

        // Token: 0x0400C092 RID: 49298
        private JobHandle m_PathfindHeuristicDeps;

        private static int MaxElevatedLength;
        private static int MaxPillarInterval;
        private static bool EnableNoPillar;
        private static bool EnableUnlimitedHeight;
        private static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_NAME);

        // Token: 0x0400C093 RID: 49299
        private PatchedNetInitializeSystem.TypeHandle __TypeHandle;

        // Token: 0x0200196C RID: 6508
        [BurstCompile]
        private struct InitializeNetDefaultsJob : IJobParallelFor
        {
            // Token: 0x060069FE RID: 27134 RVA: 0x0048BD98 File Offset: 0x00489F98
            public void Execute(int index)
            {
                ArchetypeChunk archetypeChunk = this.m_Chunks[index];
                NativeArray<NetGeometryData> nativeArray = archetypeChunk.GetNativeArray<NetGeometryData>(ref this.m_NetGeometryType);
                if (nativeArray.Length == 0)
                {
                    return;
                }
                NativeArray<NetData> nativeArray2 = archetypeChunk.GetNativeArray<NetData>(ref this.m_NetType);
                NativeArray<PlaceableNetData> nativeArray3 = archetypeChunk.GetNativeArray<PlaceableNetData>(ref this.m_PlaceableNetType);
                NativeArray<RoadData> nativeArray4 = archetypeChunk.GetNativeArray<RoadData>(ref this.m_RoadType);
                BufferAccessor<DefaultNetLane> bufferAccessor = archetypeChunk.GetBufferAccessor<DefaultNetLane>(ref this.m_DefaultNetLaneType);
                BufferAccessor<NetGeometrySection> bufferAccessor2 = archetypeChunk.GetBufferAccessor<NetGeometrySection>(ref this.m_NetGeometrySectionType);
                NativeList<NetCompositionPiece> nativeList = new NativeList<NetCompositionPiece>(32, Allocator.Temp);
                NativeList<NetCompositionLane> netLanes = new NativeList<NetCompositionLane>(32, Allocator.Temp);
                CompositionFlags flags = default(CompositionFlags);
                CompositionFlags flags2 = new CompositionFlags(CompositionFlags.General.Elevated, (CompositionFlags.Side)0U, (CompositionFlags.Side)0U);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    DynamicBuffer<NetGeometrySection> geometrySections = bufferAccessor2[i];
                    NetCompositionHelpers.GetCompositionPieces(nativeList, geometrySections.AsNativeArray(), flags, this.m_NetSubSectionData, this.m_NetSectionPieceData);
                    NetCompositionData netCompositionData = default(NetCompositionData);
                    NetCompositionHelpers.CalculateCompositionData(ref netCompositionData, nativeList.AsArray(), this.m_NetPieceData, this.m_NetLaneData, this.m_NetVertexMatchData, this.m_NetPieceLanes);
                    NetCompositionHelpers.AddCompositionLanes<NativeList<NetCompositionPiece>>(Entity.Null, ref netCompositionData, nativeList, netLanes, default(DynamicBuffer<NetCompositionCarriageway>), this.m_NetLaneData, this.m_NetPieceLanes);
                    if (bufferAccessor.Length != 0)
                    {
                        DynamicBuffer<DefaultNetLane> dynamicBuffer = bufferAccessor[i];
                        dynamicBuffer.ResizeUninitialized(netLanes.Length);
                        for (int j = 0; j < netLanes.Length; j++)
                        {
                            dynamicBuffer[j] = new DefaultNetLane(netLanes[j]);
                        }
                    }
                    NetData netData = nativeArray2[i];
                    netData.m_NodePriority += netCompositionData.m_Width;
                    NetGeometryData netGeometryData = nativeArray[i];
                    netGeometryData.m_DefaultWidth = netCompositionData.m_Width;
                    netGeometryData.m_DefaultHeightRange = netCompositionData.m_HeightRange;
                    netGeometryData.m_DefaultSurfaceHeight = netCompositionData.m_SurfaceHeight;
                    this.UpdateFlagMasks(ref netData, geometrySections);
                    if ((netData.m_RequiredLayers & (Layer.Road | Layer.TramTrack | Layer.PublicTransportRoad)) != Layer.None)
                    {
                        netData.m_GeneralFlagMask |= (CompositionFlags.General.TrafficLights | CompositionFlags.General.RemoveTrafficLights);
                        netData.m_SideFlagMask |= (CompositionFlags.Side.AddCrosswalk | CompositionFlags.Side.RemoveCrosswalk);
                    }
                    if ((netData.m_RequiredLayers & (Layer.Road | Layer.PublicTransportRoad)) != Layer.None)
                    {
                        netData.m_GeneralFlagMask |= CompositionFlags.General.AllWayStop;
                        netData.m_SideFlagMask |= (CompositionFlags.Side.ForbidLeftTurn | CompositionFlags.Side.ForbidRightTurn | CompositionFlags.Side.ForbidStraight);
                    }
                    bool flag = (netCompositionData.m_State & (CompositionState.HasForwardRoadLanes | CompositionState.HasForwardTrackLanes)) > (CompositionState)0;
                    bool flag2 = (netCompositionData.m_State & (CompositionState.HasBackwardRoadLanes | CompositionState.HasBackwardTrackLanes)) > (CompositionState)0;

                    if (flag != flag2)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.FlipTrafficHandedness;
                    }
                    if ((netCompositionData.m_State & CompositionState.Asymmetric) != (CompositionState)0)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.Asymmetric;
                    }
                    if ((netCompositionData.m_State & CompositionState.ExclusiveGround) != (CompositionState)0)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.ExclusiveGround;
                    }
                    if (nativeArray3.Length != 0 && (netGeometryData.m_Flags & GeometryFlags.RequireElevated) == (GeometryFlags)0)
                    {
                        PlaceableNetComposition placeableNetComposition = default(PlaceableNetComposition);
                        NetCompositionHelpers.CalculatePlaceableData(ref placeableNetComposition, nativeList.AsArray(), this.m_PlaceableNetPieceData);
                        this.AddObjectCosts(ref placeableNetComposition, nativeList);
                        PlaceableNetData value = nativeArray3[i];
                        value.m_DefaultConstructionCost = placeableNetComposition.m_ConstructionCost;
                        value.m_DefaultUpkeepCost = placeableNetComposition.m_UpkeepCost;
                        nativeArray3[i] = value;
                    }
                    if (nativeArray4.Length != 0)
                    {
                        RoadData roadData = nativeArray4[i];
                        if ((netCompositionData.m_State & (CompositionState.HasForwardRoadLanes | CompositionState.HasBackwardRoadLanes)) == CompositionState.HasForwardRoadLanes)
                        {
                            roadData.m_Flags |= RoadFlags.DefaultIsForward;
                        }
                        else if ((netCompositionData.m_State & (CompositionState.HasForwardRoadLanes | CompositionState.HasBackwardRoadLanes)) == CompositionState.HasBackwardRoadLanes)
                        {
                            roadData.m_Flags |= RoadFlags.DefaultIsBackward;
                        }
                        if ((roadData.m_Flags & RoadFlags.UseHighwayRules) != (RoadFlags)0)
                        {
                            netGeometryData.m_MinNodeOffset += netGeometryData.m_DefaultWidth * 0.5f;
                        }
                        nativeArray4[i] = roadData;
                    }
                    nativeList.Clear();
                    NetCompositionHelpers.GetCompositionPieces(nativeList, geometrySections.AsNativeArray(), flags2, this.m_NetSubSectionData, this.m_NetSectionPieceData);
                    NetCompositionData netCompositionData2 = default(NetCompositionData);
                    NetCompositionHelpers.CalculateCompositionData(ref netCompositionData2, nativeList.AsArray(), this.m_NetPieceData, this.m_NetLaneData, this.m_NetVertexMatchData, this.m_NetPieceLanes);
                    netGeometryData.m_ElevatedWidth = netCompositionData2.m_Width;
                    netGeometryData.m_ElevatedHeightRange = netCompositionData2.m_HeightRange;
                    if (nativeArray3.Length != 0 && (netGeometryData.m_Flags & GeometryFlags.RequireElevated) != (GeometryFlags)0)
                    {
                        PlaceableNetComposition placeableNetComposition2 = default(PlaceableNetComposition);
                        NetCompositionHelpers.CalculatePlaceableData(ref placeableNetComposition2, nativeList.AsArray(), this.m_PlaceableNetPieceData);
                        this.AddObjectCosts(ref placeableNetComposition2, nativeList);
                        PlaceableNetData value2 = nativeArray3[i];
                        value2.m_DefaultConstructionCost = placeableNetComposition2.m_ConstructionCost;
                        value2.m_DefaultUpkeepCost = placeableNetComposition2.m_UpkeepCost;
                        nativeArray3[i] = value2;
                    }
                    nativeArray2[i] = netData;
                    nativeArray[i] = netGeometryData;
                    nativeList.Clear();
                    netLanes.Clear();
                }
                nativeList.Dispose();
                netLanes.Dispose();
            }

            // Token: 0x060069FF RID: 27135 RVA: 0x0048C250 File Offset: 0x0048A450
            private void UpdateFlagMasks(ref NetData netData, DynamicBuffer<NetGeometrySection> geometrySections)
            {
                for (int i = 0; i < geometrySections.Length; i++)
                {
                    NetGeometrySection netGeometrySection = geometrySections[i];
                    netData.m_GeneralFlagMask |= netGeometrySection.m_CompositionAll.m_General;
                    netData.m_SideFlagMask |= (netGeometrySection.m_CompositionAll.m_Left | netGeometrySection.m_CompositionAll.m_Right);
                    netData.m_GeneralFlagMask |= netGeometrySection.m_CompositionAny.m_General;
                    netData.m_SideFlagMask |= (netGeometrySection.m_CompositionAny.m_Left | netGeometrySection.m_CompositionAny.m_Right);
                    netData.m_GeneralFlagMask |= netGeometrySection.m_CompositionNone.m_General;
                    netData.m_SideFlagMask |= (netGeometrySection.m_CompositionNone.m_Left | netGeometrySection.m_CompositionNone.m_Right);
                    this.UpdateFlagMasks(ref netData, netGeometrySection.m_Section);
                }
            }

            // Token: 0x06006A00 RID: 27136 RVA: 0x0048C330 File Offset: 0x0048A530
            private void UpdateFlagMasks(ref NetData netData, Entity section)
            {
                DynamicBuffer<NetSubSection> dynamicBuffer;
                if (this.m_NetSubSectionData.TryGetBuffer(section, out dynamicBuffer))
                {
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        NetSubSection netSubSection = dynamicBuffer[i];
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionAll.m_General;
                        netData.m_SideFlagMask |= (netSubSection.m_CompositionAll.m_Left | netSubSection.m_CompositionAll.m_Right);
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionAny.m_General;
                        netData.m_SideFlagMask |= (netSubSection.m_CompositionAny.m_Left | netSubSection.m_CompositionAny.m_Right);
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionNone.m_General;
                        netData.m_SideFlagMask |= (netSubSection.m_CompositionNone.m_Left | netSubSection.m_CompositionNone.m_Right);
                        this.UpdateFlagMasks(ref netData, netSubSection.m_SubSection);
                    }
                }
                DynamicBuffer<NetSectionPiece> dynamicBuffer2;
                if (this.m_NetSectionPieceData.TryGetBuffer(section, out dynamicBuffer2))
                {
                    for (int j = 0; j < dynamicBuffer2.Length; j++)
                    {
                        NetSectionPiece netSectionPiece = dynamicBuffer2[j];
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionAll.m_General;
                        netData.m_SideFlagMask |= (netSectionPiece.m_CompositionAll.m_Left | netSectionPiece.m_CompositionAll.m_Right);
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionAny.m_General;
                        netData.m_SideFlagMask |= (netSectionPiece.m_CompositionAny.m_Left | netSectionPiece.m_CompositionAny.m_Right);
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionNone.m_General;
                        netData.m_SideFlagMask |= (netSectionPiece.m_CompositionNone.m_Left | netSectionPiece.m_CompositionNone.m_Right);
                        DynamicBuffer<NetPieceObject> dynamicBuffer3;
                        if (this.m_NetPieceObjects.TryGetBuffer(netSectionPiece.m_Piece, out dynamicBuffer3))
                        {
                            for (int k = 0; k < dynamicBuffer3.Length; k++)
                            {
                                NetPieceObject netPieceObject = dynamicBuffer3[k];
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionAll.m_General;
                                netData.m_SideFlagMask |= (netPieceObject.m_CompositionAll.m_Left | netPieceObject.m_CompositionAll.m_Right);
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionAny.m_General;
                                netData.m_SideFlagMask |= (netPieceObject.m_CompositionAny.m_Left | netPieceObject.m_CompositionAny.m_Right);
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionNone.m_General;
                                netData.m_SideFlagMask |= (netPieceObject.m_CompositionNone.m_Left | netPieceObject.m_CompositionNone.m_Right);
                            }
                        }
                    }
                }
            }

            // Token: 0x06006A01 RID: 27137 RVA: 0x0048C5F0 File Offset: 0x0048A7F0
            private void AddObjectCosts(ref PlaceableNetComposition placeableCompositionData, NativeList<NetCompositionPiece> pieceBuffer)
            {
                for (int i = 0; i < pieceBuffer.Length; i++)
                {
                    NetCompositionPiece netCompositionPiece = pieceBuffer[i];
                    if (this.m_NetPieceObjects.HasBuffer(netCompositionPiece.m_Piece))
                    {
                        DynamicBuffer<NetPieceObject> dynamicBuffer = this.m_NetPieceObjects[netCompositionPiece.m_Piece];
                        for (int j = 0; j < dynamicBuffer.Length; j++)
                        {
                            NetPieceObject netPieceObject = dynamicBuffer[j];
                            if (this.m_PlaceableObjectData.HasComponent(netPieceObject.m_Prefab))
                            {
                                uint num = this.m_PlaceableObjectData[netPieceObject.m_Prefab].m_ConstructionCost;
                                if (netPieceObject.m_Spacing.z > 0.1f)
                                {
                                    num = (uint)Mathf.RoundToInt(num * (8f / netPieceObject.m_Spacing.z));
                                }
                                placeableCompositionData.m_ConstructionCost += num;
                            }
                        }
                    }
                }
            }

            // Token: 0x0400C094 RID: 49300
            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray<ArchetypeChunk> m_Chunks;

            // Token: 0x0400C095 RID: 49301
            [ReadOnly]
            public BufferTypeHandle<NetGeometrySection> m_NetGeometrySectionType;

            // Token: 0x0400C096 RID: 49302
            public ComponentTypeHandle<NetData> m_NetType;

            // Token: 0x0400C097 RID: 49303
            public ComponentTypeHandle<NetGeometryData> m_NetGeometryType;

            // Token: 0x0400C098 RID: 49304
            public ComponentTypeHandle<PlaceableNetData> m_PlaceableNetType;

            // Token: 0x0400C099 RID: 49305
            public ComponentTypeHandle<RoadData> m_RoadType;

            // Token: 0x0400C09A RID: 49306
            public BufferTypeHandle<DefaultNetLane> m_DefaultNetLaneType;

            // Token: 0x0400C09B RID: 49307
            [ReadOnly]
            public ComponentLookup<NetPieceData> m_NetPieceData;

            // Token: 0x0400C09C RID: 49308
            [ReadOnly]
            public ComponentLookup<NetLaneData> m_NetLaneData;

            // Token: 0x0400C09D RID: 49309
            [ReadOnly]
            public ComponentLookup<NetVertexMatchData> m_NetVertexMatchData;

            // Token: 0x0400C09E RID: 49310
            [ReadOnly]
            public ComponentLookup<PlaceableNetPieceData> m_PlaceableNetPieceData;

            // Token: 0x0400C09F RID: 49311
            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> m_PlaceableObjectData;

            // Token: 0x0400C0A0 RID: 49312
            [ReadOnly]
            public BufferLookup<NetSubSection> m_NetSubSectionData;

            // Token: 0x0400C0A1 RID: 49313
            [ReadOnly]
            public BufferLookup<NetSectionPiece> m_NetSectionPieceData;

            // Token: 0x0400C0A2 RID: 49314
            [ReadOnly]
            public BufferLookup<NetPieceLane> m_NetPieceLanes;

            // Token: 0x0400C0A3 RID: 49315
            [ReadOnly]
            public BufferLookup<NetPieceObject> m_NetPieceObjects;
        }

        // Token: 0x0200196D RID: 6509
        [BurstCompile]
        private struct CollectPathfindDataJob : IJobChunk
        {
            // Token: 0x06006A02 RID: 27138 RVA: 0x0048C6D0 File Offset: 0x0048A8D0
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<NetLaneData> nativeArray = chunk.GetNativeArray<NetLaneData>(ref this.m_NetLaneDataType);
                PathfindHeuristicData value = this.m_PathfindHeuristicData.value;
                if (chunk.Has<ConnectionLaneData>(ref this.m_ConnectionLaneDataType))
                {
                    for (int i = 0; i < nativeArray.Length; i++)
                    {
                        NetLaneData netLaneData = nativeArray[i];
                        PathfindConnectionData pathfindConnectionData;
                        if (this.m_PathfindConnectionData.TryGetComponent(netLaneData.m_PathfindPrefab, out pathfindConnectionData))
                        {
                            value.m_FlyingCosts.m_Value = math.min(value.m_FlyingCosts.m_Value, pathfindConnectionData.m_AirwayCost.m_Value);
                            value.m_OffRoadCosts.m_Value = math.min(value.m_OffRoadCosts.m_Value, pathfindConnectionData.m_AreaCost.m_Value);
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < nativeArray.Length; j++)
                    {
                        NetLaneData netLaneData2 = nativeArray[j];
                        if ((netLaneData2.m_Flags & LaneFlags.Road) != (LaneFlags)0)
                        {
                            PathfindCarData pathfindCarData;
                            if (this.m_PathfindCarData.TryGetComponent(netLaneData2.m_PathfindPrefab, out pathfindCarData))
                            {
                                value.m_CarCosts.m_Value = math.min(value.m_CarCosts.m_Value, pathfindCarData.m_DrivingCost.m_Value);
                            }
                            PathfindTransportData pathfindTransportData;
                            if (this.m_PathfindTransportData.TryGetComponent(netLaneData2.m_PathfindPrefab, out pathfindTransportData))
                            {
                                value.m_TaxiCosts.m_Value = math.min(value.m_TaxiCosts.m_Value, pathfindTransportData.m_TravelCost.m_Value);
                            }
                        }
                        PathfindTrackData pathfindTrackData;
                        if ((netLaneData2.m_Flags & LaneFlags.Track) != (LaneFlags)0 && this.m_PathfindTrackData.TryGetComponent(netLaneData2.m_PathfindPrefab, out pathfindTrackData))
                        {
                            value.m_TrackCosts.m_Value = math.min(value.m_TrackCosts.m_Value, pathfindTrackData.m_DrivingCost.m_Value);
                        }
                        PathfindPedestrianData pathfindPedestrianData;
                        if ((netLaneData2.m_Flags & LaneFlags.Pedestrian) != (LaneFlags)0 && this.m_PathfindPedestrianData.TryGetComponent(netLaneData2.m_PathfindPrefab, out pathfindPedestrianData))
                        {
                            value.m_PedestrianCosts.m_Value = math.min(value.m_PedestrianCosts.m_Value, pathfindPedestrianData.m_WalkingCost.m_Value);
                        }
                    }
                }
                this.m_PathfindHeuristicData.value = value;
            }

            // Token: 0x06006A03 RID: 27139 RVA: 0x00099B33 File Offset: 0x00097D33
            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                this.Execute(chunk, unfilteredChunkIndex, useEnabledMask, chunkEnabledMask);
            }

            // Token: 0x0400C0A4 RID: 49316
            [ReadOnly]
            public ComponentTypeHandle<NetLaneData> m_NetLaneDataType;

            // Token: 0x0400C0A5 RID: 49317
            [ReadOnly]
            public ComponentTypeHandle<ConnectionLaneData> m_ConnectionLaneDataType;

            // Token: 0x0400C0A6 RID: 49318
            [ReadOnly]
            public ComponentLookup<PathfindCarData> m_PathfindCarData;

            // Token: 0x0400C0A7 RID: 49319
            [ReadOnly]
            public ComponentLookup<PathfindPedestrianData> m_PathfindPedestrianData;

            // Token: 0x0400C0A8 RID: 49320
            [ReadOnly]
            public ComponentLookup<PathfindTrackData> m_PathfindTrackData;

            // Token: 0x0400C0A9 RID: 49321
            [ReadOnly]
            public ComponentLookup<PathfindTransportData> m_PathfindTransportData;

            // Token: 0x0400C0AA RID: 49322
            [ReadOnly]
            public ComponentLookup<PathfindConnectionData> m_PathfindConnectionData;

            // Token: 0x0400C0AB RID: 49323
            public NativeValue<PathfindHeuristicData> m_PathfindHeuristicData;
        }

        // Token: 0x0200196E RID: 6510
        private struct TypeHandle
        {
            // Token: 0x06006A04 RID: 27140 RVA: 0x0048C8DC File Offset: 0x0048AADC
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                this.__Game_Prefabs_PrefabData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabData>(true);
                this.__Game_Prefabs_NetData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetData>(false);
                this.__Game_Prefabs_NetPieceData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetPieceData>(false);
                this.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetGeometryData>(false);
                this.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PlaceableNetData>(false);
                this.__Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<MarkerNetData>(true);
                this.__Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<LocalConnectData>(false);
                this.__Game_Prefabs_NetLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneData>(false);
                this.__Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneGeometryData>(false);
                this.__Game_Prefabs_CarLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CarLaneData>(false);
                this.__Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TrackLaneData>(false);
                this.__Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<UtilityLaneData>(false);
                this.__Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<ParkingLaneData>(false);
                this.__Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PedestrianLaneData>(false);
                this.__Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<SecondaryLaneData>(false);
                this.__Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetCrosswalkData>(false);
                this.__Game_Prefabs_RoadData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<RoadData>(false);
                this.__Game_Prefabs_TrackData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TrackData>(false);
                this.__Game_Prefabs_WaterwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<WaterwayData>(false);
                this.__Game_Prefabs_PathwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PathwayData>(false);
                this.__Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TaxiwayData>(false);
                this.__Game_Prefabs_PowerLineData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PowerLineData>(true);
                this.__Game_Prefabs_PipelineData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PipelineData>(true);
                this.__Game_Prefabs_FenceData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<FenceData>(true);
                this.__Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<EditorContainerData>(true);
                this.__Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<ElectricityConnectionData>(false);
                this.__Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<WaterPipeConnectionData>(false);
                this.__Game_Prefabs_BridgeData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<BridgeData>(true);
                this.__Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<SpawnableObjectData>(false);
                this.__Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetTerrainData>(false);
                this.__Game_Prefabs_NetSubSection_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetSubSection>(false);
                this.__Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetSectionPiece>(false);
                this.__Game_Prefabs_NetPieceLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceLane>(false);
                this.__Game_Prefabs_NetPieceArea_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceArea>(false);
                this.__Game_Prefabs_NetPieceObject_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceObject>(false);
                this.__Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometrySection>(false);
                this.__Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometryEdgeState>(false);
                this.__Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometryNodeState>(false);
                this.__Game_Prefabs_SubObject_RW_BufferTypeHandle = state.GetBufferTypeHandle<SubObject>(false);
                this.__Game_Prefabs_SubMesh_RW_BufferTypeHandle = state.GetBufferTypeHandle<SubMesh>(false);
                this.__Game_Prefabs_FixedNetElement_RW_BufferTypeHandle = state.GetBufferTypeHandle<FixedNetElement>(false);
                this.__Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<AuxiliaryNetLane>(false);
                this.__Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometrySection>(true);
                this.__Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<DefaultNetLane>(false);
                this.__Game_Prefabs_NetPieceData_RO_ComponentLookup = state.GetComponentLookup<NetPieceData>(true);
                this.__Game_Prefabs_NetLaneData_RO_ComponentLookup = state.GetComponentLookup<NetLaneData>(true);
                this.__Game_Prefabs_NetVertexMatchData_RO_ComponentLookup = state.GetComponentLookup<NetVertexMatchData>(true);
                this.__Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup = state.GetComponentLookup<PlaceableNetPieceData>(true);
                this.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup = state.GetComponentLookup<PlaceableObjectData>(true);
                this.__Game_Prefabs_NetSubSection_RO_BufferLookup = state.GetBufferLookup<NetSubSection>(true);
                this.__Game_Prefabs_NetSectionPiece_RO_BufferLookup = state.GetBufferLookup<NetSectionPiece>(true);
                this.__Game_Prefabs_NetPieceLane_RO_BufferLookup = state.GetBufferLookup<NetPieceLane>(true);
                this.__Game_Prefabs_NetPieceObject_RO_BufferLookup = state.GetBufferLookup<NetPieceObject>(true);
                this.__Game_Prefabs_NetLaneData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneData>(true);
                this.__Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ConnectionLaneData>(true);
                this.__Game_Prefabs_PathfindCarData_RO_ComponentLookup = state.GetComponentLookup<PathfindCarData>(true);
                this.__Game_Prefabs_PathfindTrackData_RO_ComponentLookup = state.GetComponentLookup<PathfindTrackData>(true);
                this.__Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup = state.GetComponentLookup<PathfindPedestrianData>(true);
                this.__Game_Prefabs_PathfindTransportData_RO_ComponentLookup = state.GetComponentLookup<PathfindTransportData>(true);
                this.__Game_Prefabs_PathfindConnectionData_RO_ComponentLookup = state.GetComponentLookup<PathfindConnectionData>(true);
            }

            // Token: 0x0400C0AC RID: 49324
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            // Token: 0x0400C0AD RID: 49325
            [ReadOnly]
            public ComponentTypeHandle<PrefabData> __Game_Prefabs_PrefabData_RO_ComponentTypeHandle;

            // Token: 0x0400C0AE RID: 49326
            public ComponentTypeHandle<NetData> __Game_Prefabs_NetData_RW_ComponentTypeHandle;

            // Token: 0x0400C0AF RID: 49327
            public ComponentTypeHandle<NetPieceData> __Game_Prefabs_NetPieceData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B0 RID: 49328
            public ComponentTypeHandle<NetGeometryData> __Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B1 RID: 49329
            public ComponentTypeHandle<PlaceableNetData> __Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B2 RID: 49330
            [ReadOnly]
            public ComponentTypeHandle<MarkerNetData> __Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle;

            // Token: 0x0400C0B3 RID: 49331
            public ComponentTypeHandle<LocalConnectData> __Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B4 RID: 49332
            public ComponentTypeHandle<NetLaneData> __Game_Prefabs_NetLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B5 RID: 49333
            public ComponentTypeHandle<NetLaneGeometryData> __Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B6 RID: 49334
            public ComponentTypeHandle<CarLaneData> __Game_Prefabs_CarLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B7 RID: 49335
            public ComponentTypeHandle<TrackLaneData> __Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B8 RID: 49336
            public ComponentTypeHandle<UtilityLaneData> __Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0B9 RID: 49337
            public ComponentTypeHandle<ParkingLaneData> __Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BA RID: 49338
            public ComponentTypeHandle<PedestrianLaneData> __Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BB RID: 49339
            public ComponentTypeHandle<SecondaryLaneData> __Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BC RID: 49340
            public ComponentTypeHandle<NetCrosswalkData> __Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BD RID: 49341
            public ComponentTypeHandle<RoadData> __Game_Prefabs_RoadData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BE RID: 49342
            public ComponentTypeHandle<TrackData> __Game_Prefabs_TrackData_RW_ComponentTypeHandle;

            // Token: 0x0400C0BF RID: 49343
            public ComponentTypeHandle<WaterwayData> __Game_Prefabs_WaterwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C0C0 RID: 49344
            public ComponentTypeHandle<PathwayData> __Game_Prefabs_PathwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C0C1 RID: 49345
            public ComponentTypeHandle<TaxiwayData> __Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C0C2 RID: 49346
            [ReadOnly]
            public ComponentTypeHandle<PowerLineData> __Game_Prefabs_PowerLineData_RO_ComponentTypeHandle;

            // Token: 0x0400C0C3 RID: 49347
            [ReadOnly]
            public ComponentTypeHandle<PipelineData> __Game_Prefabs_PipelineData_RO_ComponentTypeHandle;

            // Token: 0x0400C0C4 RID: 49348
            [ReadOnly]
            public ComponentTypeHandle<FenceData> __Game_Prefabs_FenceData_RO_ComponentTypeHandle;

            // Token: 0x0400C0C5 RID: 49349
            [ReadOnly]
            public ComponentTypeHandle<EditorContainerData> __Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle;

            // Token: 0x0400C0C6 RID: 49350
            public ComponentTypeHandle<ElectricityConnectionData> __Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle;

            // Token: 0x0400C0C7 RID: 49351
            public ComponentTypeHandle<WaterPipeConnectionData> __Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle;

            // Token: 0x0400C0C8 RID: 49352
            [ReadOnly]
            public ComponentTypeHandle<BridgeData> __Game_Prefabs_BridgeData_RO_ComponentTypeHandle;

            // Token: 0x0400C0C9 RID: 49353
            public ComponentTypeHandle<SpawnableObjectData> __Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle;

            // Token: 0x0400C0CA RID: 49354
            public ComponentTypeHandle<NetTerrainData> __Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle;

            // Token: 0x0400C0CB RID: 49355
            public BufferTypeHandle<NetSubSection> __Game_Prefabs_NetSubSection_RW_BufferTypeHandle;

            // Token: 0x0400C0CC RID: 49356
            public BufferTypeHandle<NetSectionPiece> __Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle;

            // Token: 0x0400C0CD RID: 49357
            public BufferTypeHandle<NetPieceLane> __Game_Prefabs_NetPieceLane_RW_BufferTypeHandle;

            // Token: 0x0400C0CE RID: 49358
            public BufferTypeHandle<NetPieceArea> __Game_Prefabs_NetPieceArea_RW_BufferTypeHandle;

            // Token: 0x0400C0CF RID: 49359
            public BufferTypeHandle<NetPieceObject> __Game_Prefabs_NetPieceObject_RW_BufferTypeHandle;

            // Token: 0x0400C0D0 RID: 49360
            public BufferTypeHandle<NetGeometrySection> __Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle;

            // Token: 0x0400C0D1 RID: 49361
            public BufferTypeHandle<NetGeometryEdgeState> __Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle;

            // Token: 0x0400C0D2 RID: 49362
            public BufferTypeHandle<NetGeometryNodeState> __Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle;

            // Token: 0x0400C0D3 RID: 49363
            public BufferTypeHandle<SubObject> __Game_Prefabs_SubObject_RW_BufferTypeHandle;

            // Token: 0x0400C0D4 RID: 49364
            public BufferTypeHandle<SubMesh> __Game_Prefabs_SubMesh_RW_BufferTypeHandle;

            // Token: 0x0400C0D5 RID: 49365
            public BufferTypeHandle<FixedNetElement> __Game_Prefabs_FixedNetElement_RW_BufferTypeHandle;

            // Token: 0x0400C0D6 RID: 49366
            public BufferTypeHandle<AuxiliaryNetLane> __Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle;

            // Token: 0x0400C0D7 RID: 49367
            [ReadOnly]
            public BufferTypeHandle<NetGeometrySection> __Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle;

            // Token: 0x0400C0D8 RID: 49368
            public BufferTypeHandle<DefaultNetLane> __Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle;

            // Token: 0x0400C0D9 RID: 49369
            [ReadOnly]
            public ComponentLookup<NetPieceData> __Game_Prefabs_NetPieceData_RO_ComponentLookup;

            // Token: 0x0400C0DA RID: 49370
            [ReadOnly]
            public ComponentLookup<NetLaneData> __Game_Prefabs_NetLaneData_RO_ComponentLookup;

            // Token: 0x0400C0DB RID: 49371
            [ReadOnly]
            public ComponentLookup<NetVertexMatchData> __Game_Prefabs_NetVertexMatchData_RO_ComponentLookup;

            // Token: 0x0400C0DC RID: 49372
            [ReadOnly]
            public ComponentLookup<PlaceableNetPieceData> __Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup;

            // Token: 0x0400C0DD RID: 49373
            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> __Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;

            // Token: 0x0400C0DE RID: 49374
            [ReadOnly]
            public BufferLookup<NetSubSection> __Game_Prefabs_NetSubSection_RO_BufferLookup;

            // Token: 0x0400C0DF RID: 49375
            [ReadOnly]
            public BufferLookup<NetSectionPiece> __Game_Prefabs_NetSectionPiece_RO_BufferLookup;

            // Token: 0x0400C0E0 RID: 49376
            [ReadOnly]
            public BufferLookup<NetPieceLane> __Game_Prefabs_NetPieceLane_RO_BufferLookup;

            // Token: 0x0400C0E1 RID: 49377
            [ReadOnly]
            public BufferLookup<NetPieceObject> __Game_Prefabs_NetPieceObject_RO_BufferLookup;

            // Token: 0x0400C0E2 RID: 49378
            [ReadOnly]
            public ComponentTypeHandle<NetLaneData> __Game_Prefabs_NetLaneData_RO_ComponentTypeHandle;

            // Token: 0x0400C0E3 RID: 49379
            [ReadOnly]
            public ComponentTypeHandle<ConnectionLaneData> __Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle;

            // Token: 0x0400C0E4 RID: 49380
            [ReadOnly]
            public ComponentLookup<PathfindCarData> __Game_Prefabs_PathfindCarData_RO_ComponentLookup;

            // Token: 0x0400C0E5 RID: 49381
            [ReadOnly]
            public ComponentLookup<PathfindTrackData> __Game_Prefabs_PathfindTrackData_RO_ComponentLookup;

            // Token: 0x0400C0E6 RID: 49382
            [ReadOnly]
            public ComponentLookup<PathfindPedestrianData> __Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup;

            // Token: 0x0400C0E7 RID: 49383
            [ReadOnly]
            public ComponentLookup<PathfindTransportData> __Game_Prefabs_PathfindTransportData_RO_ComponentLookup;

            // Token: 0x0400C0E8 RID: 49384
            [ReadOnly]
            public ComponentLookup<PathfindConnectionData> __Game_Prefabs_PathfindConnectionData_RO_ComponentLookup;
        }
    }
}
