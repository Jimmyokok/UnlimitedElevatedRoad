using Colossal;
using Colossal.IO.AssetDatabase;
using ConfigurableElevatedRoad.Systems;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;
using Unity.Entities;


namespace ConfigurableElevatedRoad
{
    [FileLocation(nameof(ConfigurableElevatedRoad))]
    [SettingsUIGroupOrder(kEnabledGroup, kLengthRoadGroup, kSteepnessGroup, kResetGroup)]
    [SettingsUIShowGroupName(kEnabledGroup, kLengthRoadGroup, kSteepnessGroup, kResetGroup)]
    public class Setting : ModSetting
    {
        public const string kSectionGeneral = "General";

        public const string kEnabledGroup = "Enabled";
        public const string kResetGroup = "Reset";
        public const string kLengthRoadGroup = "Length Settings";
        public const string kSteepnessGroup = "Steepness Restrictions";


        public Setting(IMod mod) : base(mod)
        {
        }
        /*
        [SettingsUISection(kSection, kButtonGroup)]
        public bool Button { set { Mod.log.Info("Button clicked"); } }
        */

        public bool nopillar_enabled = false;
        public bool noheight_enabled = false;
        public bool use_default_elevated_length = false;
        public bool use_default_pillar_interval = false;
        public bool unbound_steepness = false;
        public int maxElevatedLength = 200;
        public int maxPillarInterval = 200;

        [SettingsUISection(kSectionGeneral, kEnabledGroup)]
        public bool Enabled
        {
            get => nopillar_enabled;

            set
            {
                nopillar_enabled = value;
                Contra = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
            }
        }

        [SettingsUISection(kSectionGeneral, kEnabledGroup)]
        public bool EnableUnlimitedHeight
        {
            get => noheight_enabled;

            set
            {
                noheight_enabled = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
            }
        }

        [SettingsUISection(kSectionGeneral, kSteepnessGroup)]
        public bool UnboundSteepness
        {
            get => unbound_steepness;

            set
            {
                unbound_steepness = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
                netCompositionDataFixSystem.unbound_steepness = value;
            }
        }

        [SettingsUISection(kSectionGeneral, kLengthRoadGroup)]
        public bool UseDefaultElevatedLength
        {
            get => use_default_elevated_length;

            set
            {
                use_default_elevated_length = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
                netCompositionDataFixSystem.use_default_elevated_length = value;
            }
        }

        [SettingsUISection(kSectionGeneral, kLengthRoadGroup)]
        public bool UseDefaultPillarInterval
        {
            get => use_default_pillar_interval;

            set
            {
                use_default_pillar_interval = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
                netCompositionDataFixSystem.use_default_pillar_interval = value;
            }
        }

        [SettingsUIHidden]
        public bool Contra { get; set; } = true;
        public bool MaxElevatedLengthDisabled() => use_default_elevated_length;
        public bool MaxPillarIntervalDisabled() => use_default_pillar_interval;

        [SettingsUISection(kSectionGeneral, kLengthRoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(MaxElevatedLengthDisabled))]
        [SettingsUISlider(min = 20, max = 800, step = 10)]
        public int MaxElevatedLength {
            get => maxElevatedLength;
            set
            {
                maxElevatedLength = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
            }
        }

        [SettingsUISection(kSectionGeneral, kLengthRoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(MaxPillarIntervalDisabled))]
        [SettingsUISlider(min = 20, max = 800, step = 10)]
        public int MaxPillarInterval
        {
            get => maxPillarInterval;
            set
            {
                maxPillarInterval = value;
                NetCompositionDataFixSystem netCompositionDataFixSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NetCompositionDataFixSystem>();
                netCompositionDataFixSystem.need_update = true;
            }
        }

        [SettingsUISection(kResetGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool reset
        {
            set
            {
                SetDefaults();
                Contra = nopillar_enabled;
                ApplyAndSave();
            }
        }

        public override void SetDefaults()
        {
            Enabled = false;
            EnableUnlimitedHeight = false;
            MaxElevatedLength = 200;
            MaxPillarInterval = 200;
            UseDefaultPillarInterval = false;
            UseDefaultElevatedLength = false;
            UnboundSteepness = false;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Configurable Elevated Road v1.0" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSectionGeneral), "Configurations" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kEnabledGroup), "Enabling Options" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kResetGroup), "Reset Configuration" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLengthRoadGroup), "Length Configurations" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kSteepnessGroup), "Steepness Configurations" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Enabled)), "Enable no pillar construction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Enabled)), "Enable no pillar construction." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.reset)), "Reset Configurations" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.reset)), "Resets setting to mod default values." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.reset)), "Confirm reset setting?" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableUnlimitedHeight)), "Enable unlimited height construction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableUnlimitedHeight)), "Enable unlimited height construction." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxElevatedLength)), "Maximum elevated length" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxElevatedLength)), "Override maximum length for continuous road/track/bridge edges." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxPillarInterval)), "Pillar interval" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxPillarInterval)), "Override pillar interval for road/track/bridge roads." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseDefaultElevatedLength)), "Use vanilla elevated length" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UseDefaultElevatedLength)), "Enable usage of vanilla elevated length." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseDefaultPillarInterval)), "Use vanilla pillar interval" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UseDefaultPillarInterval)), "Enable usage of pillar interval." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UnboundSteepness)), "Lift Steepness Restrictions" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UnboundSteepness)), "Lift steepness restrictions for road constructions." },
                };
        }
        public void Unload()
        {

        }
    }

    public class LocaleCN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleCN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "可调节高架道路 v1.0" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSectionGeneral), "调节选项" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kEnabledGroup), "启停选项" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kResetGroup), "重置选项" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLengthRoadGroup), "长度选项" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kSteepnessGroup), "坡度选项" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Enabled)), "启用无桥墩建造模式" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Enabled)), "启用无桥墩建造模式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.reset)), "重置全部设置" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.reset)), "重置全部设置" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.reset)), "是否重置到MOD默认设置?" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableUnlimitedHeight)), "启用无限高度建造模式" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableUnlimitedHeight)), "启用无限高度建造模式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxElevatedLength)), "最大区段长度" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxElevatedLength)), "设置道路、轨道、桥梁和电缆的最大区段长度" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxPillarInterval)), "桥墩间距" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxPillarInterval)), "设置高架道路、轨道、桥梁和电缆的桥墩间距" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseDefaultElevatedLength)), "使用原版区段长度" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UseDefaultElevatedLength)), "使用原版区段长度" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseDefaultPillarInterval)), "使用原版桥墩间距" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UseDefaultPillarInterval)), "使用原版桥墩间距" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UnboundSteepness)), "启用无限坡度建造模式" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UnboundSteepness)), "启用无限坡度建造模式" },
                };
        }
        public void Unload()
        {

        }
    }
}
