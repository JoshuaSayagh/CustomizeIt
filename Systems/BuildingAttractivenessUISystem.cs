using Colossal.Logging;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Entities;

namespace CustomizeIt.Systems
{
    /// <summary>
    /// Handles the attractiveness editor panel UI and communicates
    /// user changes to the override system.
    /// </summary>
    public partial class BuildingAttractivenessUISystem : UISystemBase
    {
        private const string kGroup = "customizeIt";

        private static new readonly ILog log = Mod.log;

        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private PrefabSystem m_PrefabSystem;
        private AttractivenessOverrideSystem m_OverrideSystem;

        private Entity m_PanelPrefabEntity;
        private Entity m_LastSelectedEntity;
        private bool m_PanelOpen;

        private ValueBinding<bool> m_HasBuildingBinding;
        private ValueBinding<int> m_AttractivenessBinding;
        private ValueBinding<string> m_BuildingNameBinding;
        private ValueBinding<bool> m_HasOverrideBinding;
        private ValueBinding<int> m_BaseAttractivenessBinding;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_OverrideSystem = World.GetOrCreateSystemManaged<AttractivenessOverrideSystem>();

            m_HasBuildingBinding = new ValueBinding<bool>(kGroup, "hasBuilding", false);
            AddBinding(m_HasBuildingBinding);

            m_AttractivenessBinding = new ValueBinding<int>(kGroup, "attractiveness", 0);
            AddBinding(m_AttractivenessBinding);

            m_BuildingNameBinding = new ValueBinding<string>(kGroup, "buildingName", "");
            AddBinding(m_BuildingNameBinding);

            m_HasOverrideBinding = new ValueBinding<bool>(kGroup, "hasOverride", false);
            AddBinding(m_HasOverrideBinding);

            m_BaseAttractivenessBinding = new ValueBinding<int>(kGroup, "baseAttractiveness", 0);
            AddBinding(m_BaseAttractivenessBinding);

            AddBinding(new TriggerBinding<int>(kGroup, "setAttractiveness", OnSetAttractiveness));
            AddBinding(new TriggerBinding(kGroup, "restoreDefault", OnRestoreDefault));
            AddBinding(new TriggerBinding(kGroup, "closePanel", OnClosePanel));

            log.Info("BuildingAttractivenessUISystem created.");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            Entity selectedEntity = m_SelectedInfoUISystem.selectedEntity;

            if (selectedEntity == m_LastSelectedEntity)
            {
                if (m_PanelOpen && m_PanelPrefabEntity != Entity.Null)
                {
                    RefreshPanelData(m_PanelPrefabEntity);
                }
                return;
            }

            m_LastSelectedEntity = selectedEntity;

            if (selectedEntity != Entity.Null
                && EntityManager.HasComponent<Building>(selectedEntity)
                && EntityManager.HasComponent<PrefabRef>(selectedEntity))
            {
                PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(selectedEntity);
                Entity prefabEntity = prefabRef.m_Prefab;

                if (EntityManager.HasComponent<AttractionData>(prefabEntity))
                {
                    m_PanelPrefabEntity = prefabEntity;
                    m_PanelOpen = true;

                    string name = GetPrefabName(prefabEntity);
                    m_HasBuildingBinding.Update(true);
                    m_BuildingNameBinding.Update(name);
                    RefreshPanelData(prefabEntity);

                    log.Info($"Selected building with prefab: {name}");
                    return;
                }
            }

            if (selectedEntity != Entity.Null)
            {
                ClosePanel();
            }
        }

        private void RefreshPanelData(Entity prefabEntity)
        {
            bool hasOverride = m_OverrideSystem.TryGetOverride(prefabEntity, out int overrideValue);
            int baseValue = m_OverrideSystem.GetBaseAttractiveness(prefabEntity);

            int displayValue;
            if (hasOverride)
            {
                displayValue = overrideValue;
            }
            else
            {
                displayValue = EntityManager.HasComponent<AttractionData>(prefabEntity)
                    ? EntityManager.GetComponentData<AttractionData>(prefabEntity).m_Attractiveness
                    : baseValue;
            }

            m_AttractivenessBinding.Update(displayValue);
            m_HasOverrideBinding.Update(hasOverride);
            m_BaseAttractivenessBinding.Update(baseValue);
        }

        private void OnSetAttractiveness(int newValue)
        {
            if (m_PanelPrefabEntity == Entity.Null || !EntityManager.Exists(m_PanelPrefabEntity))
                return;

            m_OverrideSystem.SetOverride(m_PanelPrefabEntity, newValue);

            m_AttractivenessBinding.Update(newValue);
            m_HasOverrideBinding.Update(true);
            log.Info($"Set attractiveness to {newValue} for prefab {m_PanelPrefabEntity.Index}");
        }

        private void OnRestoreDefault()
        {
            if (m_PanelPrefabEntity == Entity.Null || !EntityManager.Exists(m_PanelPrefabEntity))
                return;

            m_OverrideSystem.RemoveOverride(m_PanelPrefabEntity);

            int baseValue = m_OverrideSystem.GetBaseAttractiveness(m_PanelPrefabEntity);
            m_AttractivenessBinding.Update(baseValue);
            m_HasOverrideBinding.Update(false);

            log.Info($"Restored default attractiveness for prefab {m_PanelPrefabEntity.Index}");
        }

        private void OnClosePanel()
        {
            ClosePanel();
        }

        private void ClosePanel()
        {
            m_PanelOpen = false;
            m_PanelPrefabEntity = Entity.Null;
            m_HasBuildingBinding.Update(false);
            m_AttractivenessBinding.Update(0);
            m_BuildingNameBinding.Update("");
            m_HasOverrideBinding.Update(false);
            m_BaseAttractivenessBinding.Update(0);
        }

        private string GetPrefabName(Entity prefabEntity)
        {
            if (m_PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefab))
            {
                return prefab.name;
            }
            return $"Building {prefabEntity.Index}";
        }
    }
}
