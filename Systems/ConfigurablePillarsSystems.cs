using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
using Game;
using Game.Prefabs;
using System.Collections.Generic;
using Game.Net;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Unity.Jobs;
using UnityEngine;
using RoadFlags = Game.Prefabs.RoadFlags;


namespace ConfigurableElevatedRoad.Systems
{

    public partial class NetCompositionDataFixSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_PrefabQuery;
        public bool need_update = true;
        public bool use_default_elevated_length = false;
        public bool use_default_pillar_interval = false;
        public bool unbound_steepness = false;

        private TypeHandle __TypeHandle;
        private Dictionary<string, float> elevate_max_dict;
        private Dictionary<string, float> elevate_min_dict;
        private Dictionary<string, float> pillar_interval_dict;
        private Dictionary<string, float> max_edge_length_dict;
        private Dictionary<string, float> max_steepness_dict;
        private Dictionary<string, float> min_edge_length_dict;

        private void override_vanilla_value<T>(string prefab_name, ref Dictionary<string, T> cache_dict, ref T value, T override_value, bool save = true)
        {
            if (!cache_dict.ContainsKey(prefab_name) && save)
            {
                cache_dict[prefab_name] = value;
            }
            value = override_value;
        }

        private void restore_vanilla_value<T>(string prefab_name, ref Dictionary<string, T> cache_dict, ref T value)
        {
            if (!cache_dict.ContainsKey(prefab_name))
            {
                cache_dict[prefab_name] = value;
            }
            else
            {
                value = cache_dict[prefab_name];
            }

        }

        private void update_individual(string name, ref NetGeometryData netGeometryData)
        {
            if (unbound_steepness)
            {
                override_vanilla_value(name, ref max_steepness_dict, ref netGeometryData.m_MaxSlopeSteepness, 100f);
            }
            else
            {
                restore_vanilla_value(name, ref max_steepness_dict, ref netGeometryData.m_MaxSlopeSteepness);
            }
            if (!use_default_elevated_length)
            {
                override_vanilla_value(name, ref max_edge_length_dict, ref netGeometryData.m_EdgeLengthRange.max, Mod.setting.maxElevatedLength);
                override_vanilla_value(name, ref min_edge_length_dict, ref netGeometryData.m_EdgeLengthRange.min, 0f);
            }
            else
            {
                restore_vanilla_value(name, ref max_edge_length_dict, ref netGeometryData.m_EdgeLengthRange.max);
                restore_vanilla_value(name, ref min_edge_length_dict, ref netGeometryData.m_EdgeLengthRange.min);
            }
            if (!use_default_pillar_interval)
            {
                override_vanilla_value(name, ref pillar_interval_dict, ref netGeometryData.m_ElevatedLength, Mod.setting.maxPillarInterval);
            }
            else
            {
                restore_vanilla_value(name, ref pillar_interval_dict, ref netGeometryData.m_ElevatedLength);
            }
        }
        protected override void OnCreate()
        {
            base.OnCreate();
            elevate_max_dict = new Dictionary<string, float>();
            elevate_min_dict = new Dictionary<string, float>();
            pillar_interval_dict = new Dictionary<string, float>();
            max_edge_length_dict = new Dictionary<string, float>();
            min_edge_length_dict = new Dictionary<string, float>();
            max_steepness_dict = new Dictionary<string, float>();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabQuery = GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
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
            RequireForUpdate(m_PrefabQuery);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
        }

        // Token: 0x060069FC RID: 27132 RVA: 0x00099B0E File Offset: 0x00097D0E
        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __AssignQueries(ref CheckedStateRef);
            __TypeHandle.__AssignHandles(ref CheckedStateRef);
        }

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
                NetGeometrySection elem = default;
                elem.m_Section = m_PrefabSystem.GetEntity(netSectionInfo.m_Section);
                elem.m_Offset = netSectionInfo.m_Offset;
                elem.m_Flags = flags;
                NetSectionFlags netSectionFlags;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireAll, out elem.m_CompositionAll, out netSectionFlags);
                NetSectionFlags netSectionFlags2;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireAny, out elem.m_CompositionAny, out netSectionFlags2);
                NetSectionFlags netSectionFlags3;
                NetCompositionHelpers.GetRequirementFlags(netSectionInfo.m_RequireNone, out elem.m_CompositionNone, out netSectionFlags3);
                NetSectionFlags netSectionFlags4 = netSectionFlags | netSectionFlags2 | netSectionFlags3;
                if (netSectionFlags4 != 0)
                {
                    baseLog.ErrorFormat(prefab, "Net section ({0}: {1}) cannot require section flags: {2}", prefab.name, netSectionInfo.m_Section.name, netSectionFlags4);
                }
                if (netSectionInfo.m_Invert)
                {
                    elem.m_Flags |= NetSectionFlags.Invert;
                }
                if (netSectionInfo.m_Flip)
                {
                    elem.m_Flags |= NetSectionFlags.FlipLanes | NetSectionFlags.FlipMesh;
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

        [Preserve]
        protected override void OnUpdate()
        {
            InstantUpdate();
        }
        public void InstantUpdate()
        {
            if (!need_update)
            {
                return;
            }
            need_update = false;
            NativeArray<ArchetypeChunk> chunks = m_PrefabQuery.ToArchetypeChunkArray(Allocator.TempJob);
            try
            {
                __TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref CheckedStateRef);
                EntityTypeHandle _Unity_Entities_Entity_TypeHandle = __TypeHandle.__Unity_Entities_Entity_TypeHandle;
                __TypeHandle.__Game_Prefabs_PrefabData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PrefabData> _Game_Prefabs_PrefabData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PrefabData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetData> _Game_Prefabs_NetData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetPieceData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetPieceData> _Game_Prefabs_NetPieceData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetPieceData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetGeometryData> _Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PlaceableNetData> _Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<MarkerNetData> _Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<LocalConnectData> _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetLaneData> _Game_Prefabs_NetLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetLaneGeometryData> _Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_CarLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<CarLaneData> _Game_Prefabs_CarLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_CarLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<TrackLaneData> _Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<UtilityLaneData> _Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<ParkingLaneData> _Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PedestrianLaneData> _Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<SecondaryLaneData> _Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetCrosswalkData> _Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<RoadData> _Game_Prefabs_RoadData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_TrackData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<TrackData> _Game_Prefabs_TrackData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_TrackData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_WaterwayData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<WaterwayData> _Game_Prefabs_WaterwayData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_WaterwayData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_PathwayData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PathwayData> _Game_Prefabs_PathwayData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PathwayData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<TaxiwayData> _Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_PowerLineData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PowerLineData> _Game_Prefabs_PowerLineData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PowerLineData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_PipelineData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<PipelineData> _Game_Prefabs_PipelineData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_PipelineData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_FenceData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<FenceData> _Game_Prefabs_FenceData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_FenceData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<EditorContainerData> _Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<ElectricityConnectionData> _Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<WaterPipeConnectionData> _Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_BridgeData_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<BridgeData> _Game_Prefabs_BridgeData_RO_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_BridgeData_RO_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<SpawnableObjectData> _Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
                ComponentTypeHandle<NetTerrainData> _Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle = __TypeHandle.__Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle;
                __TypeHandle.__Game_Prefabs_NetSubSection_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetSubSection> _Game_Prefabs_NetSubSection_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetSubSection_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetSectionPiece> _Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetPieceLane_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetPieceLane> _Game_Prefabs_NetPieceLane_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetPieceLane_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetPieceArea_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetPieceArea> _Game_Prefabs_NetPieceArea_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetPieceArea_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetPieceObject_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetPieceObject> _Game_Prefabs_NetPieceObject_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetPieceObject_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetGeometrySection> _Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetGeometryEdgeState> _Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<NetGeometryNodeState> _Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_SubObject_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<SubObject> _Game_Prefabs_SubObject_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_SubObject_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_SubMesh_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<SubMesh> _Game_Prefabs_SubMesh_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_SubMesh_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_FixedNetElement_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<FixedNetElement> _Game_Prefabs_FixedNetElement_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_FixedNetElement_RW_BufferTypeHandle;
                __TypeHandle.__Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle.Update(ref CheckedStateRef);
                BufferTypeHandle<AuxiliaryNetLane> _Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle = __TypeHandle.__Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle;
                CompleteDependency();
                for (int i = 0; i < chunks.Length; i++)
                {
                    ArchetypeChunk archetypeChunk = chunks[i];
                    NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(_Unity_Entities_Entity_TypeHandle);
                    NativeArray<PrefabData> nativeArray2 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_PrefabData_RO_ComponentTypeHandle);
                    bool flag = archetypeChunk.Has(ref _Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle);
                    bool flag2 = archetypeChunk.Has(ref _Game_Prefabs_BridgeData_RO_ComponentTypeHandle);
                    NativeArray<NetData> nativeArray3 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_NetData_RW_ComponentTypeHandle);
                    NativeArray<NetGeometryData> nativeArray4 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle);
                    NativeArray<PlaceableNetData> nativeArray5 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle);
                    if (nativeArray4.Length != 0)
                    {
                        BufferAccessor<NetGeometrySection> bufferAccessor = archetypeChunk.GetBufferAccessor(ref _Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle);
                        for (int j = 0; j < nativeArray4.Length; j++)
                        {
                            Entity entity = nativeArray[j];
                            NetGeometryPrefab prefab = m_PrefabSystem.GetPrefab<NetGeometryPrefab>(nativeArray2[j]);
                            NetGeometryData value = nativeArray4[j];
                            DynamicBuffer<NetGeometrySection> target = bufferAccessor[j];
                            value.m_EdgeLengthRange.max = 200f;
                            value.m_ElevatedLength = 80f;
                            value.m_MaxSlopeSteepness = math.select(prefab.m_MaxSlopeSteepness, 0f, prefab.m_MaxSlopeSteepness < 0.001f);
                            value.m_ElevationLimit = 4f;
                            target.Clear();
                            AddSections(prefab, prefab.m_Sections, target, 0);
                            UndergroundNetSections component = prefab.GetComponent<UndergroundNetSections>();
                            if (component != null)
                            {
                                if (!Mod.setting.nopillar_enabled || prefab.name == "Hydroelectric_Power_Plant_01 Dam")
                                {
                                    AddSections(prefab, component.m_Sections, target, NetSectionFlags.Underground);
                                }
                            }
                            OverheadNetSections component2 = prefab.GetComponent<OverheadNetSections>();
                            if (component2 != null)
                            {
                                if (!Mod.setting.nopillar_enabled || prefab.name == "Hydroelectric_Power_Plant_01 Dam")
                                {
                                    AddSections(prefab, component2.m_Sections, target, NetSectionFlags.Overhead);
                                }
                            }
                        }
                    }
                    for (int n = 0; n < nativeArray5.Length; n++)
                    {
                        NetPrefab prefab3 = m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[n]);

                        PlaceableNet component3 = prefab3.GetComponent<PlaceableNet>();
                        if (component3 != null)
                        {
                            PlaceableNetData placeableNetData = nativeArray5[n];
                            if (component3.m_ElevationRange.max >= 20f)
                            {
                                if (Mod.setting.noheight_enabled)
                                {
                                    override_vanilla_value(prefab3.name, ref elevate_max_dict, ref component3.m_ElevationRange.max, 1000f);
                                    override_vanilla_value(prefab3.name, ref elevate_max_dict, ref placeableNetData.m_ElevationRange.max, 1000f, false);
                                }
                                else
                                {
                                    restore_vanilla_value(prefab3.name, ref elevate_max_dict, ref component3.m_ElevationRange.max);
                                    restore_vanilla_value(prefab3.name, ref elevate_max_dict, ref placeableNetData.m_ElevationRange.max);
                                }
                            }

                            if (component3.m_ElevationRange.min == -20f)
                            {
                                if (Mod.setting.noheight_enabled)
                                {
                                    override_vanilla_value(prefab3.name, ref elevate_min_dict, ref component3.m_ElevationRange.min, -50f);
                                    override_vanilla_value(prefab3.name, ref elevate_min_dict, ref placeableNetData.m_ElevationRange.min, -50f, false);
                                }
                                else
                                {
                                    restore_vanilla_value(prefab3.name, ref elevate_min_dict, ref component3.m_ElevationRange.min);
                                    restore_vanilla_value(prefab3.name, ref elevate_min_dict, ref placeableNetData.m_ElevationRange.min);
                                }
                            }
                            nativeArray5[n] = placeableNetData;
                        }
                    }
                    BufferAccessor<SubObject> bufferAccessor4 = archetypeChunk.GetBufferAccessor(ref _Game_Prefabs_SubObject_RW_BufferTypeHandle);
                    for (int num = 0; num < bufferAccessor4.Length; num++)
                    {
                        NetSubObjects component5 = m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[num]).GetComponent<NetSubObjects>();
                        DynamicBuffer<SubObject> dynamicBuffer3 = bufferAccessor4[num];
                        dynamicBuffer3.Clear();
                        for (int num2 = 0; num2 < component5.m_SubObjects.Length; num2++)
                        {
                            NetSubObjectInfo netSubObjectInfo = component5.m_SubObjects[num2];
                            ObjectPrefab @object = netSubObjectInfo.m_Object;

                            string prefab_name = m_PrefabSystem.GetPrefab<NetPrefab>(nativeArray2[num]).name;
                            if (!Mod.setting.nopillar_enabled || netSubObjectInfo.m_RequireOutsideConnection || prefab_name == "Hydroelectric_Power_Plant_01 Dam")
                            {
                                bool flag3 = false;
                                SubObject subObject = default;
                                NetGeometryData value2 = default;
                                if (nativeArray4.Length != 0)
                                {
                                    value2 = nativeArray4[num];
                                }
                                subObject.m_Prefab = m_PrefabSystem.GetEntity(@object);
                                subObject.m_Position = netSubObjectInfo.m_Position;
                                subObject.m_Rotation = netSubObjectInfo.m_Rotation;
                                subObject.m_Probability = 100;
                                switch (netSubObjectInfo.m_Placement)
                                {
                                    case NetObjectPlacement.EdgeEndsOrNode:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine;
                                        break;
                                    case NetObjectPlacement.EdgeMiddle:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.MiddlePlacement;
                                        if (EntityManager.HasComponent<PillarData>(subObject.m_Prefab))
                                        {
                                            value2.m_Flags |= GeometryFlags.MiddlePillars;
                                        }
                                        break;
                                    case NetObjectPlacement.EdgeEnds:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement;
                                        break;
                                    case NetObjectPlacement.CourseStart:
                                        subObject.m_Flags |= SubObjectFlags.CoursePlacement | SubObjectFlags.StartPlacement;
                                        if (!flag3)
                                        {
                                            subObject.m_Flags |= SubObjectFlags.MakeOwner;
                                            value2.m_Flags |= GeometryFlags.SubOwner;
                                            flag3 = true;
                                        }
                                        break;
                                    case NetObjectPlacement.CourseEnd:
                                        subObject.m_Flags |= SubObjectFlags.CoursePlacement | SubObjectFlags.EndPlacement;
                                        if (!flag3)
                                        {
                                            subObject.m_Flags |= SubObjectFlags.MakeOwner;
                                            value2.m_Flags |= GeometryFlags.SubOwner;
                                            flag3 = true;
                                        }
                                        break;
                                    case NetObjectPlacement.NodeBeforeFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.NodeBetweenFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.NodeAfterFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeMiddleFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.MiddlePlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        if (EntityManager.HasComponent<PillarData>(subObject.m_Prefab))
                                        {
                                            value2.m_Flags |= GeometryFlags.MiddlePillars;
                                        }
                                        break;
                                    case NetObjectPlacement.EdgeEndsFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeStartFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeEndFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeEndsOrNodeFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeStartOrNodeFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.StartPlacement | SubObjectFlags.FixedPlacement;
                                        subObject.m_ParentIndex = netSubObjectInfo.m_FixedIndex;
                                        break;
                                    case NetObjectPlacement.EdgeEndOrNodeFixedSegment:
                                        subObject.m_Flags |= SubObjectFlags.EdgePlacement | SubObjectFlags.AllowCombine | SubObjectFlags.EndPlacement | SubObjectFlags.FixedPlacement;
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
                                }
                                if (netSubObjectInfo.m_RequireDeadEnd)
                                {
                                    subObject.m_Flags |= SubObjectFlags.RequireDeadEnd;
                                }
                                if (netSubObjectInfo.m_RequireOrphan)
                                {
                                    subObject.m_Flags |= SubObjectFlags.RequireOrphan;
                                }
                                dynamicBuffer3.Add(subObject);
                            }
                        }
                    }

                    NativeArray<RoadData> nativeArray17 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_RoadData_RW_ComponentTypeHandle);
                    if (nativeArray17.Length != 0)
                    {
                        for (int num19 = 0; num19 < nativeArray17.Length; num19++)
                        {
                            RoadPrefab prefab9 = m_PrefabSystem.GetPrefab<RoadPrefab>(nativeArray2[num19]);
                            NetData value13 = nativeArray3[num19];
                            NetGeometryData netGeometryData = nativeArray4[num19];
                            RoadData value14 = nativeArray17[num19];
                            RoadType roadType = prefab9.m_RoadType;
                            update_individual(prefab9.name, ref netGeometryData);
                            nativeArray4[num19] = netGeometryData;
                        }
                    }
                    NativeArray<TrackData> nativeArray18 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_TrackData_RW_ComponentTypeHandle);
                    if (nativeArray18.Length != 0)
                    {
                        int num20 = 0;
                        while (num20 < nativeArray18.Length)
                        {
                            TrackPrefab prefab10 = m_PrefabSystem.GetPrefab<TrackPrefab>(nativeArray2[num20]);
                            NetData value16 = nativeArray3[num20];
                            NetGeometryData netGeometryData2 = nativeArray4[num20];
                            TrackData value17 = nativeArray18[num20];
                            update_individual(prefab10.name, ref netGeometryData2);
                            nativeArray4[num20] = netGeometryData2;
                            num20++;
                        }
                    }
                    NativeArray<PathwayData> nativeArray20 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_PathwayData_RW_ComponentTypeHandle);
                    if (nativeArray20.Length != 0)
                    {
                        NativeArray<LocalConnectData> nativeArray21 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle);
                        for (int num23 = 0; num23 < nativeArray20.Length; num23++)
                        {
                            PathwayPrefab prefab12 = m_PrefabSystem.GetPrefab<PathwayPrefab>(nativeArray2[num23]);
                            NetData value23 = nativeArray3[num23];
                            NetGeometryData netGeometryData3 = nativeArray4[num23];
                            LocalConnectData value24 = nativeArray21[num23];
                            PathwayData value25 = nativeArray20[num23];
                            update_individual(prefab12.name, ref netGeometryData3);
                            nativeArray4[num23] = netGeometryData3;
                        }
                    }
                    NativeArray<TaxiwayData> nativeArray22 = archetypeChunk.GetNativeArray(ref _Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle);
                    if (nativeArray22.Length != 0)
                    {
                        for (int num24 = 0; num24 < nativeArray22.Length; num24++)
                        {
                            TaxiwayPrefab prefab13 = m_PrefabSystem.GetPrefab<TaxiwayPrefab>(nativeArray2[num24]);
                            NetData value27 = nativeArray3[num24];
                            NetGeometryData value28 = nativeArray4[num24];
                            TaxiwayData value29 = nativeArray22[num24];
                            update_individual(prefab13.name, ref value28);
                            nativeArray4[num24] = value28;
                        }
                    }
                    bool flag8 = archetypeChunk.Has(ref _Game_Prefabs_PowerLineData_RO_ComponentTypeHandle);
                    if (flag8)
                    {
                        for (int num25 = 0; num25 < nativeArray.Length; num25++)
                        {
                            PowerLinePrefab prefab14 = m_PrefabSystem.GetPrefab<PowerLinePrefab>(nativeArray2[num25]);
                            NetGeometryData value31 = nativeArray4[num25];
                            update_individual(prefab14.name, ref value31);
                        }
                    }
                    if (flag2)
                    {
                        BufferAccessor<FixedNetElement> bufferAccessor12 = archetypeChunk.GetBufferAccessor(ref _Game_Prefabs_FixedNetElement_RW_BufferTypeHandle);
                        for (int num29 = 0; num29 < nativeArray4.Length; num29++)
                        {
                            NetGeometryPrefab prefab16 = m_PrefabSystem.GetPrefab<NetGeometryPrefab>(nativeArray2[num29]);
                            Bridge component23 = prefab16.GetComponent<Bridge>();
                            NetData value36 = nativeArray3[num29];
                            NetGeometryData netGeometryData6 = nativeArray4[num29];
                            update_individual(prefab16.name, ref netGeometryData6);
                            nativeArray4[num29] = netGeometryData6;
                        }
                    }
                }
            }
            catch
            {
                chunks.Dispose();
                throw;
            }
            __TypeHandle.__Game_Prefabs_NetPieceObject_RO_BufferLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetPieceLane_RO_BufferLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetSectionPiece_RO_BufferLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetSubSection_RO_BufferLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetVertexMatchData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetPieceData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle.Update(ref CheckedStateRef);
            InitializeNetDefaultsJob initializeNetDefaultsJob = default;
            initializeNetDefaultsJob.m_Chunks = chunks;
            initializeNetDefaultsJob.m_NetGeometrySectionType = __TypeHandle.__Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle;
            initializeNetDefaultsJob.m_NetType = __TypeHandle.__Game_Prefabs_NetData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_NetGeometryType = __TypeHandle.__Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_PlaceableNetType = __TypeHandle.__Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_RoadType = __TypeHandle.__Game_Prefabs_RoadData_RW_ComponentTypeHandle;
            initializeNetDefaultsJob.m_DefaultNetLaneType = __TypeHandle.__Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle;
            initializeNetDefaultsJob.m_NetPieceData = __TypeHandle.__Game_Prefabs_NetPieceData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetLaneData = __TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetVertexMatchData = __TypeHandle.__Game_Prefabs_NetVertexMatchData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_PlaceableNetPieceData = __TypeHandle.__Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_PlaceableObjectData = __TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;
            initializeNetDefaultsJob.m_NetSubSectionData = __TypeHandle.__Game_Prefabs_NetSubSection_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetSectionPieceData = __TypeHandle.__Game_Prefabs_NetSectionPiece_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetPieceLanes = __TypeHandle.__Game_Prefabs_NetPieceLane_RO_BufferLookup;
            initializeNetDefaultsJob.m_NetPieceObjects = __TypeHandle.__Game_Prefabs_NetPieceObject_RO_BufferLookup;
            InitializeNetDefaultsJob jobData = initializeNetDefaultsJob;
            Dependency = jobData.Schedule(chunks.Length, 1, Dependency);
        }

        private struct InitializeNetDefaultsJob : IJobParallelFor
        {
            // Token: 0x060069FE RID: 27134 RVA: 0x0048BD98 File Offset: 0x00489F98
            public void Execute(int index)
            {
                ArchetypeChunk archetypeChunk = m_Chunks[index];
                NativeArray<NetGeometryData> nativeArray = archetypeChunk.GetNativeArray(ref m_NetGeometryType);
                if (nativeArray.Length == 0)
                {
                    return;
                }
                NativeArray<NetData> nativeArray2 = archetypeChunk.GetNativeArray(ref m_NetType);
                NativeArray<PlaceableNetData> nativeArray3 = archetypeChunk.GetNativeArray(ref m_PlaceableNetType);
                NativeArray<RoadData> nativeArray4 = archetypeChunk.GetNativeArray(ref m_RoadType);
                BufferAccessor<DefaultNetLane> bufferAccessor = archetypeChunk.GetBufferAccessor(ref m_DefaultNetLaneType);
                BufferAccessor<NetGeometrySection> bufferAccessor2 = archetypeChunk.GetBufferAccessor(ref m_NetGeometrySectionType);
                NativeList<NetCompositionPiece> nativeList = new NativeList<NetCompositionPiece>(32, Allocator.Temp);
                NativeList<NetCompositionLane> netLanes = new NativeList<NetCompositionLane>(32, Allocator.Temp);
                CompositionFlags flags = default;
                CompositionFlags flags2 = new CompositionFlags(CompositionFlags.General.Elevated, 0U, 0U);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    DynamicBuffer<NetGeometrySection> geometrySections = bufferAccessor2[i];
                    NetCompositionHelpers.GetCompositionPieces(nativeList, geometrySections.AsNativeArray(), flags, m_NetSubSectionData, m_NetSectionPieceData);
                    NetCompositionData netCompositionData = default;
                    NetCompositionHelpers.CalculateCompositionData(ref netCompositionData, nativeList.AsArray(), m_NetPieceData, m_NetLaneData, m_NetVertexMatchData, m_NetPieceLanes);
                    NetCompositionHelpers.AddCompositionLanes(Entity.Null, ref netCompositionData, nativeList, netLanes, default, m_NetLaneData, m_NetPieceLanes);
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
                    UpdateFlagMasks(ref netData, geometrySections);
                    if ((netData.m_RequiredLayers & (Layer.Road | Layer.TramTrack | Layer.PublicTransportRoad)) != Layer.None)
                    {
                        netData.m_GeneralFlagMask |= CompositionFlags.General.TrafficLights | CompositionFlags.General.RemoveTrafficLights;
                        netData.m_SideFlagMask |= CompositionFlags.Side.AddCrosswalk | CompositionFlags.Side.RemoveCrosswalk;
                    }
                    if ((netData.m_RequiredLayers & (Layer.Road | Layer.PublicTransportRoad)) != Layer.None)
                    {
                        netData.m_GeneralFlagMask |= CompositionFlags.General.AllWayStop;
                        netData.m_SideFlagMask |= CompositionFlags.Side.ForbidLeftTurn | CompositionFlags.Side.ForbidRightTurn | CompositionFlags.Side.ForbidStraight;
                    }
                    bool flag = (netCompositionData.m_State & (CompositionState.HasForwardRoadLanes | CompositionState.HasForwardTrackLanes)) > 0;
                    bool flag2 = (netCompositionData.m_State & (CompositionState.HasBackwardRoadLanes | CompositionState.HasBackwardTrackLanes)) > 0;

                    if (flag != flag2)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.FlipTrafficHandedness;
                    }
                    if ((netCompositionData.m_State & CompositionState.Asymmetric) != 0)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.Asymmetric;
                    }
                    if ((netCompositionData.m_State & CompositionState.ExclusiveGround) != 0)
                    {
                        netGeometryData.m_Flags |= GeometryFlags.ExclusiveGround;
                    }
                    if (nativeArray3.Length != 0 && (netGeometryData.m_Flags & GeometryFlags.RequireElevated) == 0)
                    {
                        PlaceableNetComposition placeableNetComposition = default;
                        NetCompositionHelpers.CalculatePlaceableData(ref placeableNetComposition, nativeList.AsArray(), m_PlaceableNetPieceData);
                        AddObjectCosts(ref placeableNetComposition, nativeList);
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
                        if ((roadData.m_Flags & RoadFlags.UseHighwayRules) != 0)
                        {
                            netGeometryData.m_MinNodeOffset += netGeometryData.m_DefaultWidth * 0.5f;
                        }
                        nativeArray4[i] = roadData;
                    }
                    nativeList.Clear();
                    NetCompositionHelpers.GetCompositionPieces(nativeList, geometrySections.AsNativeArray(), flags2, m_NetSubSectionData, m_NetSectionPieceData);
                    NetCompositionData netCompositionData2 = default;
                    NetCompositionHelpers.CalculateCompositionData(ref netCompositionData2, nativeList.AsArray(), m_NetPieceData, m_NetLaneData, m_NetVertexMatchData, m_NetPieceLanes);
                    netGeometryData.m_ElevatedWidth = netCompositionData2.m_Width;
                    netGeometryData.m_ElevatedHeightRange = netCompositionData2.m_HeightRange;
                    if (nativeArray3.Length != 0 && (netGeometryData.m_Flags & GeometryFlags.RequireElevated) != 0)
                    {
                        PlaceableNetComposition placeableNetComposition2 = default;
                        NetCompositionHelpers.CalculatePlaceableData(ref placeableNetComposition2, nativeList.AsArray(), m_PlaceableNetPieceData);
                        AddObjectCosts(ref placeableNetComposition2, nativeList);
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
                    netData.m_SideFlagMask |= netGeometrySection.m_CompositionAll.m_Left | netGeometrySection.m_CompositionAll.m_Right;
                    netData.m_GeneralFlagMask |= netGeometrySection.m_CompositionAny.m_General;
                    netData.m_SideFlagMask |= netGeometrySection.m_CompositionAny.m_Left | netGeometrySection.m_CompositionAny.m_Right;
                    netData.m_GeneralFlagMask |= netGeometrySection.m_CompositionNone.m_General;
                    netData.m_SideFlagMask |= netGeometrySection.m_CompositionNone.m_Left | netGeometrySection.m_CompositionNone.m_Right;
                    UpdateFlagMasks(ref netData, netGeometrySection.m_Section);
                }
            }

            // Token: 0x06006A00 RID: 27136 RVA: 0x0048C330 File Offset: 0x0048A530
            private void UpdateFlagMasks(ref NetData netData, Entity section)
            {
                DynamicBuffer<NetSubSection> dynamicBuffer;
                if (m_NetSubSectionData.TryGetBuffer(section, out dynamicBuffer))
                {
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        NetSubSection netSubSection = dynamicBuffer[i];
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionAll.m_General;
                        netData.m_SideFlagMask |= netSubSection.m_CompositionAll.m_Left | netSubSection.m_CompositionAll.m_Right;
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionAny.m_General;
                        netData.m_SideFlagMask |= netSubSection.m_CompositionAny.m_Left | netSubSection.m_CompositionAny.m_Right;
                        netData.m_GeneralFlagMask |= netSubSection.m_CompositionNone.m_General;
                        netData.m_SideFlagMask |= netSubSection.m_CompositionNone.m_Left | netSubSection.m_CompositionNone.m_Right;
                        UpdateFlagMasks(ref netData, netSubSection.m_SubSection);
                    }
                }
                DynamicBuffer<NetSectionPiece> dynamicBuffer2;
                if (m_NetSectionPieceData.TryGetBuffer(section, out dynamicBuffer2))
                {
                    for (int j = 0; j < dynamicBuffer2.Length; j++)
                    {
                        NetSectionPiece netSectionPiece = dynamicBuffer2[j];
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionAll.m_General;
                        netData.m_SideFlagMask |= netSectionPiece.m_CompositionAll.m_Left | netSectionPiece.m_CompositionAll.m_Right;
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionAny.m_General;
                        netData.m_SideFlagMask |= netSectionPiece.m_CompositionAny.m_Left | netSectionPiece.m_CompositionAny.m_Right;
                        netData.m_GeneralFlagMask |= netSectionPiece.m_CompositionNone.m_General;
                        netData.m_SideFlagMask |= netSectionPiece.m_CompositionNone.m_Left | netSectionPiece.m_CompositionNone.m_Right;
                        DynamicBuffer<NetPieceObject> dynamicBuffer3;
                        if (m_NetPieceObjects.TryGetBuffer(netSectionPiece.m_Piece, out dynamicBuffer3))
                        {
                            for (int k = 0; k < dynamicBuffer3.Length; k++)
                            {
                                NetPieceObject netPieceObject = dynamicBuffer3[k];
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionAll.m_General;
                                netData.m_SideFlagMask |= netPieceObject.m_CompositionAll.m_Left | netPieceObject.m_CompositionAll.m_Right;
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionAny.m_General;
                                netData.m_SideFlagMask |= netPieceObject.m_CompositionAny.m_Left | netPieceObject.m_CompositionAny.m_Right;
                                netData.m_GeneralFlagMask |= netPieceObject.m_CompositionNone.m_General;
                                netData.m_SideFlagMask |= netPieceObject.m_CompositionNone.m_Left | netPieceObject.m_CompositionNone.m_Right;
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
                    if (m_NetPieceObjects.HasBuffer(netCompositionPiece.m_Piece))
                    {
                        DynamicBuffer<NetPieceObject> dynamicBuffer = m_NetPieceObjects[netCompositionPiece.m_Piece];
                        for (int j = 0; j < dynamicBuffer.Length; j++)
                        {
                            NetPieceObject netPieceObject = dynamicBuffer[j];
                            if (m_PlaceableObjectData.HasComponent(netPieceObject.m_Prefab))
                            {
                                uint num = m_PlaceableObjectData[netPieceObject.m_Prefab].m_ConstructionCost;
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
        public NetCompositionDataFixSystem()
        {
        }

        private struct TypeHandle
        {
            // Token: 0x060070F0 RID: 28912 RVA: 0x0044F638 File Offset: 0x0044D838
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                __Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                __Game_Prefabs_PrefabData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabData>(true);
                __Game_Prefabs_NetData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetData>(false);
                __Game_Prefabs_NetPieceData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetPieceData>(false);
                __Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetGeometryData>(false);
                __Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PlaceableNetData>(false);
                __Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<MarkerNetData>(true);
                __Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<LocalConnectData>(false);
                __Game_Prefabs_NetLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneData>(false);
                __Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneGeometryData>(false);
                __Game_Prefabs_CarLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CarLaneData>(false);
                __Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TrackLaneData>(false);
                __Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<UtilityLaneData>(false);
                __Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<ParkingLaneData>(false);
                __Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PedestrianLaneData>(false);
                __Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<SecondaryLaneData>(false);
                __Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetCrosswalkData>(false);
                __Game_Prefabs_RoadData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<RoadData>(false);
                __Game_Prefabs_TrackData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TrackData>(false);
                __Game_Prefabs_WaterwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<WaterwayData>(false);
                __Game_Prefabs_PathwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PathwayData>(false);
                __Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TaxiwayData>(false);
                __Game_Prefabs_PowerLineData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PowerLineData>(true);
                __Game_Prefabs_PipelineData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PipelineData>(true);
                __Game_Prefabs_FenceData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<FenceData>(true);
                __Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<EditorContainerData>(true);
                __Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<ElectricityConnectionData>(false);
                __Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<WaterPipeConnectionData>(false);
                __Game_Prefabs_BridgeData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<BridgeData>(true);
                __Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<SpawnableObjectData>(false);
                __Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<NetTerrainData>(false);
                __Game_Prefabs_NetSubSection_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetSubSection>(false);
                __Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetSectionPiece>(false);
                __Game_Prefabs_NetPieceLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceLane>(false);
                __Game_Prefabs_NetPieceArea_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceArea>(false);
                __Game_Prefabs_NetPieceObject_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetPieceObject>(false);
                __Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometrySection>(false);
                __Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometryEdgeState>(false);
                __Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometryNodeState>(false);
                __Game_Prefabs_SubObject_RW_BufferTypeHandle = state.GetBufferTypeHandle<SubObject>(false);
                __Game_Prefabs_SubMesh_RW_BufferTypeHandle = state.GetBufferTypeHandle<SubMesh>(false);
                __Game_Prefabs_FixedNetElement_RW_BufferTypeHandle = state.GetBufferTypeHandle<FixedNetElement>(false);
                __Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<AuxiliaryNetLane>(false);
                __Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle = state.GetBufferTypeHandle<NetGeometrySection>(true);
                __Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle = state.GetBufferTypeHandle<DefaultNetLane>(false);
                __Game_Prefabs_NetPieceData_RO_ComponentLookup = state.GetComponentLookup<NetPieceData>(true);
                __Game_Prefabs_NetLaneData_RO_ComponentLookup = state.GetComponentLookup<NetLaneData>(true);
                __Game_Prefabs_NetVertexMatchData_RO_ComponentLookup = state.GetComponentLookup<NetVertexMatchData>(true);
                __Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup = state.GetComponentLookup<PlaceableNetPieceData>(true);
                __Game_Prefabs_PlaceableObjectData_RO_ComponentLookup = state.GetComponentLookup<PlaceableObjectData>(true);
                __Game_Prefabs_NetSubSection_RO_BufferLookup = state.GetBufferLookup<NetSubSection>(true);
                __Game_Prefabs_NetSectionPiece_RO_BufferLookup = state.GetBufferLookup<NetSectionPiece>(true);
                __Game_Prefabs_NetPieceLane_RO_BufferLookup = state.GetBufferLookup<NetPieceLane>(true);
                __Game_Prefabs_NetPieceObject_RO_BufferLookup = state.GetBufferLookup<NetPieceObject>(true);
                __Game_Prefabs_NetLaneData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<NetLaneData>(true);
                __Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ConnectionLaneData>(true);
                __Game_Prefabs_PathfindCarData_RO_ComponentLookup = state.GetComponentLookup<PathfindCarData>(true);
                __Game_Prefabs_PathfindTrackData_RO_ComponentLookup = state.GetComponentLookup<PathfindTrackData>(true);
                __Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup = state.GetComponentLookup<PathfindPedestrianData>(true);
                __Game_Prefabs_PathfindTransportData_RO_ComponentLookup = state.GetComponentLookup<PathfindTransportData>(true);
                __Game_Prefabs_PathfindConnectionData_RO_ComponentLookup = state.GetComponentLookup<PathfindConnectionData>(true);
            }

            // Token: 0x0400C648 RID: 50760
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            // Token: 0x0400C649 RID: 50761
            [ReadOnly]
            public ComponentTypeHandle<PrefabData> __Game_Prefabs_PrefabData_RO_ComponentTypeHandle;

            // Token: 0x0400C64A RID: 50762
            public ComponentTypeHandle<NetData> __Game_Prefabs_NetData_RW_ComponentTypeHandle;

            // Token: 0x0400C64B RID: 50763
            public ComponentTypeHandle<NetPieceData> __Game_Prefabs_NetPieceData_RW_ComponentTypeHandle;

            // Token: 0x0400C64C RID: 50764
            public ComponentTypeHandle<NetGeometryData> __Game_Prefabs_NetGeometryData_RW_ComponentTypeHandle;

            // Token: 0x0400C64D RID: 50765
            public ComponentTypeHandle<PlaceableNetData> __Game_Prefabs_PlaceableNetData_RW_ComponentTypeHandle;

            // Token: 0x0400C64E RID: 50766
            [ReadOnly]
            public ComponentTypeHandle<MarkerNetData> __Game_Prefabs_MarkerNetData_RO_ComponentTypeHandle;

            // Token: 0x0400C64F RID: 50767
            public ComponentTypeHandle<LocalConnectData> __Game_Prefabs_LocalConnectData_RW_ComponentTypeHandle;

            // Token: 0x0400C650 RID: 50768
            public ComponentTypeHandle<NetLaneData> __Game_Prefabs_NetLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C651 RID: 50769
            public ComponentTypeHandle<NetLaneGeometryData> __Game_Prefabs_NetLaneGeometryData_RW_ComponentTypeHandle;

            // Token: 0x0400C652 RID: 50770
            public ComponentTypeHandle<CarLaneData> __Game_Prefabs_CarLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C653 RID: 50771
            public ComponentTypeHandle<TrackLaneData> __Game_Prefabs_TrackLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C654 RID: 50772
            public ComponentTypeHandle<UtilityLaneData> __Game_Prefabs_UtilityLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C655 RID: 50773
            public ComponentTypeHandle<ParkingLaneData> __Game_Prefabs_ParkingLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C656 RID: 50774
            public ComponentTypeHandle<PedestrianLaneData> __Game_Prefabs_PedestrianLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C657 RID: 50775
            public ComponentTypeHandle<SecondaryLaneData> __Game_Prefabs_SecondaryLaneData_RW_ComponentTypeHandle;

            // Token: 0x0400C658 RID: 50776
            public ComponentTypeHandle<NetCrosswalkData> __Game_Prefabs_NetCrosswalkData_RW_ComponentTypeHandle;

            // Token: 0x0400C659 RID: 50777
            public ComponentTypeHandle<RoadData> __Game_Prefabs_RoadData_RW_ComponentTypeHandle;

            // Token: 0x0400C65A RID: 50778
            public ComponentTypeHandle<TrackData> __Game_Prefabs_TrackData_RW_ComponentTypeHandle;

            // Token: 0x0400C65B RID: 50779
            public ComponentTypeHandle<WaterwayData> __Game_Prefabs_WaterwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C65C RID: 50780
            public ComponentTypeHandle<PathwayData> __Game_Prefabs_PathwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C65D RID: 50781
            public ComponentTypeHandle<TaxiwayData> __Game_Prefabs_TaxiwayData_RW_ComponentTypeHandle;

            // Token: 0x0400C65E RID: 50782
            [ReadOnly]
            public ComponentTypeHandle<PowerLineData> __Game_Prefabs_PowerLineData_RO_ComponentTypeHandle;

            // Token: 0x0400C65F RID: 50783
            [ReadOnly]
            public ComponentTypeHandle<PipelineData> __Game_Prefabs_PipelineData_RO_ComponentTypeHandle;

            // Token: 0x0400C660 RID: 50784
            [ReadOnly]
            public ComponentTypeHandle<FenceData> __Game_Prefabs_FenceData_RO_ComponentTypeHandle;

            // Token: 0x0400C661 RID: 50785
            [ReadOnly]
            public ComponentTypeHandle<EditorContainerData> __Game_Prefabs_EditorContainerData_RO_ComponentTypeHandle;

            // Token: 0x0400C662 RID: 50786
            public ComponentTypeHandle<ElectricityConnectionData> __Game_Prefabs_ElectricityConnectionData_RW_ComponentTypeHandle;

            // Token: 0x0400C663 RID: 50787
            public ComponentTypeHandle<WaterPipeConnectionData> __Game_Prefabs_WaterPipeConnectionData_RW_ComponentTypeHandle;

            // Token: 0x0400C664 RID: 50788
            [ReadOnly]
            public ComponentTypeHandle<BridgeData> __Game_Prefabs_BridgeData_RO_ComponentTypeHandle;

            // Token: 0x0400C665 RID: 50789
            public ComponentTypeHandle<SpawnableObjectData> __Game_Prefabs_SpawnableObjectData_RW_ComponentTypeHandle;

            // Token: 0x0400C666 RID: 50790
            public ComponentTypeHandle<NetTerrainData> __Game_Prefabs_NetTerrainData_RW_ComponentTypeHandle;

            // Token: 0x0400C667 RID: 50791
            public BufferTypeHandle<NetSubSection> __Game_Prefabs_NetSubSection_RW_BufferTypeHandle;

            // Token: 0x0400C668 RID: 50792
            public BufferTypeHandle<NetSectionPiece> __Game_Prefabs_NetSectionPiece_RW_BufferTypeHandle;

            // Token: 0x0400C669 RID: 50793
            public BufferTypeHandle<NetPieceLane> __Game_Prefabs_NetPieceLane_RW_BufferTypeHandle;

            // Token: 0x0400C66A RID: 50794
            public BufferTypeHandle<NetPieceArea> __Game_Prefabs_NetPieceArea_RW_BufferTypeHandle;

            // Token: 0x0400C66B RID: 50795
            public BufferTypeHandle<NetPieceObject> __Game_Prefabs_NetPieceObject_RW_BufferTypeHandle;

            // Token: 0x0400C66C RID: 50796
            public BufferTypeHandle<NetGeometrySection> __Game_Prefabs_NetGeometrySection_RW_BufferTypeHandle;

            // Token: 0x0400C66D RID: 50797
            public BufferTypeHandle<NetGeometryEdgeState> __Game_Prefabs_NetGeometryEdgeState_RW_BufferTypeHandle;

            // Token: 0x0400C66E RID: 50798
            public BufferTypeHandle<NetGeometryNodeState> __Game_Prefabs_NetGeometryNodeState_RW_BufferTypeHandle;

            // Token: 0x0400C66F RID: 50799
            public BufferTypeHandle<SubObject> __Game_Prefabs_SubObject_RW_BufferTypeHandle;

            // Token: 0x0400C670 RID: 50800
            public BufferTypeHandle<SubMesh> __Game_Prefabs_SubMesh_RW_BufferTypeHandle;

            // Token: 0x0400C671 RID: 50801
            public BufferTypeHandle<FixedNetElement> __Game_Prefabs_FixedNetElement_RW_BufferTypeHandle;

            // Token: 0x0400C672 RID: 50802
            public BufferTypeHandle<AuxiliaryNetLane> __Game_Prefabs_AuxiliaryNetLane_RW_BufferTypeHandle;

            // Token: 0x0400C673 RID: 50803
            [ReadOnly]
            public BufferTypeHandle<NetGeometrySection> __Game_Prefabs_NetGeometrySection_RO_BufferTypeHandle;

            // Token: 0x0400C674 RID: 50804
            public BufferTypeHandle<DefaultNetLane> __Game_Prefabs_DefaultNetLane_RW_BufferTypeHandle;

            // Token: 0x0400C675 RID: 50805
            [ReadOnly]
            public ComponentLookup<NetPieceData> __Game_Prefabs_NetPieceData_RO_ComponentLookup;

            // Token: 0x0400C676 RID: 50806
            [ReadOnly]
            public ComponentLookup<NetLaneData> __Game_Prefabs_NetLaneData_RO_ComponentLookup;

            // Token: 0x0400C677 RID: 50807
            [ReadOnly]
            public ComponentLookup<NetVertexMatchData> __Game_Prefabs_NetVertexMatchData_RO_ComponentLookup;

            // Token: 0x0400C678 RID: 50808
            [ReadOnly]
            public ComponentLookup<PlaceableNetPieceData> __Game_Prefabs_PlaceableNetPieceData_RO_ComponentLookup;

            // Token: 0x0400C679 RID: 50809
            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> __Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;

            // Token: 0x0400C67A RID: 50810
            [ReadOnly]
            public BufferLookup<NetSubSection> __Game_Prefabs_NetSubSection_RO_BufferLookup;

            // Token: 0x0400C67B RID: 50811
            [ReadOnly]
            public BufferLookup<NetSectionPiece> __Game_Prefabs_NetSectionPiece_RO_BufferLookup;

            // Token: 0x0400C67C RID: 50812
            [ReadOnly]
            public BufferLookup<NetPieceLane> __Game_Prefabs_NetPieceLane_RO_BufferLookup;

            // Token: 0x0400C67D RID: 50813
            [ReadOnly]
            public BufferLookup<NetPieceObject> __Game_Prefabs_NetPieceObject_RO_BufferLookup;

            // Token: 0x0400C67E RID: 50814
            [ReadOnly]
            public ComponentTypeHandle<NetLaneData> __Game_Prefabs_NetLaneData_RO_ComponentTypeHandle;

            // Token: 0x0400C67F RID: 50815
            [ReadOnly]
            public ComponentTypeHandle<ConnectionLaneData> __Game_Prefabs_ConnectionLaneData_RO_ComponentTypeHandle;

            // Token: 0x0400C680 RID: 50816
            [ReadOnly]
            public ComponentLookup<PathfindCarData> __Game_Prefabs_PathfindCarData_RO_ComponentLookup;

            // Token: 0x0400C681 RID: 50817
            [ReadOnly]
            public ComponentLookup<PathfindTrackData> __Game_Prefabs_PathfindTrackData_RO_ComponentLookup;

            // Token: 0x0400C682 RID: 50818
            [ReadOnly]
            public ComponentLookup<PathfindPedestrianData> __Game_Prefabs_PathfindPedestrianData_RO_ComponentLookup;

            // Token: 0x0400C683 RID: 50819
            [ReadOnly]
            public ComponentLookup<PathfindTransportData> __Game_Prefabs_PathfindTransportData_RO_ComponentLookup;

            // Token: 0x0400C684 RID: 50820
            [ReadOnly]
            public ComponentLookup<PathfindConnectionData> __Game_Prefabs_PathfindConnectionData_RO_ComponentLookup;
        }
    } // class

} // namespace