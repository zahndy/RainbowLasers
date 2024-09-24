using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Operators;
using FrooxEngine.FrooxEngine.ProtoFlux.CoreNodes;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Color;

namespace RainbowLasers
{

    public class Patch : ResoniteMod
    {
        public override String Name => "RainbowLasers";
        public override String Author => "zahndy";
        public override String Link => "https://github.com/zahndy/RainbowLasers";
        public override String Version => "1.0.0";
 
        public static ModConfiguration config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> BASE_COLOR = new ModConfigurationKey<colorX>("BASE_COLOR", "Base color", () => new colorX(.25F, 1F, 1F, 1F));
        [Range(0, 1)]
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> OFFSET = new ModConfigurationKey<float>("Offset", "Offset Between start and end of laser", () => 0.3f);

        public override void OnEngineInit()

        {
            config = GetConfiguration();
            config.Save(true);
            Harmony harmony = new Harmony("com.zahndy.RainbowLasers");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(InteractionLaser))]
        class InteractionLaserJank
        {
            [HarmonyPrefix]
            [HarmonyPatch("OnAwake")]
            static void Prefix(InteractionLaser __instance,
                    FieldDrive<colorX> ____startColor,
                    FieldDrive<colorX> ____endColor,
                    FieldDrive<float3> ____directPoint,
                    FieldDrive<float3> ____actualPoint)
            {
                __instance.RunInUpdates(3, () =>
                {
                    if (!config.GetValue(ENABLED)) return;
                    if (__instance.Slot.ActiveUserRoot.ActiveUser != __instance.LocalUser) return;

                    Slot Assets = __instance.Slot.AddSlot("Assets");
                    ValueField<colorX> ColE = Assets.AttachComponent<ValueField<colorX>>(); //End
                    ValueField<colorX> ColS = Assets.AttachComponent<ValueField<colorX>>(); //Start
                    BentTubeMesh Mesh = Assets.AttachComponent<BentTubeMesh>();
                    AssetRef<Mesh> Renderer = __instance.Slot.GetComponent<MeshRenderer>().Mesh;

                    Renderer.Target = Mesh;
                    Mesh.Radius.Value = 0.002f;
                    Mesh.Sides.Value = 6;
                    Mesh.Segments.Value = 16;

                    ____startColor.Value = ColS.Value.ReferenceID;
                    ____endColor.Value = ColE.Value.ReferenceID;

                    ____directPoint.ForceLink(Mesh.DirectTargetPoint);
                    ____actualPoint.ForceLink(Mesh.ActualTargetPoint);

                    bool IsRight = __instance.Side == Chirality.Right;

                    Mesh.StartPointColor.Value = new colorX(.25f, 1f, 1f, 1f);
                    Mesh.EndPointColor.Value = new colorX(.25f, 1f, 1f, 1f);

                    ValueField<colorX> Start = SetUpLogix(Assets, ColS.Value, Mesh.StartPointColor, true, IsRight); 
                    ValueField<colorX> End = SetUpLogix(Assets, ColE.Value, Mesh.EndPointColor, false, IsRight);
                    // ____startColor / BASE_COLOR * DesiredSource 


                    __instance.Enabled = true;
                });
            }

        }

        private static ValueField<colorX> SetUpLogix(Slot root,
                IField<colorX> Input,
                Sync<colorX> Field,
                bool IsStart,
                bool IsRight)
        {
            Slot driver = root.AddSlot(IsStart ? "Start" : "End");
            // Field <= (Input / Default) * Desired
            ValueSource<colorX> InputSource = driver.AttachComponent<ValueSource<colorX>>();
            InputSource.TrySetRootSource(Input);

            ValueField<colorX> Default = driver.AttachComponent<ValueField<colorX>>();       //Static Default Value 
            Default.Value.Value = config.GetValue(BASE_COLOR);
            ValueSource<colorX> DefaultSource = driver.AttachComponent<ValueSource<colorX>>();
            DefaultSource.TrySetRootSource(Default.Value);                 

            ValueField<colorX> DesiredField = driver.AttachComponent<ValueField<colorX>>(); //target
            DesiredField.Value.Value = new colorX(.25f, 1f, 1f, 1f); 
            ValueSource<colorX> DesiredSource = driver.AttachComponent<ValueSource<colorX>>();
            DesiredSource.TrySetRootSource(DesiredField.Value);


            WorldTimeTenthFloat time = driver.AttachComponent<WorldTimeTenthFloat>();            
            ColorXHue mid = driver.AttachComponent<ColorXHue>();
            //TODO ValueMul for adjustable rgb speed
            if (IsStart)
            {
                ValueField<float> offsetField = driver.AttachComponent<ValueField<float>>();
                if (IsRight)
                {
                    offsetField.Value.Value = -config.GetValue(OFFSET);
                }
                else
                {
                    offsetField.Value.Value = config.GetValue(OFFSET);
                }
                ValueSource<float> offsetSource = driver.AttachComponent<ValueSource<float>>();
                offsetSource.TrySetRootSource(offsetField.Value);

                ValueAdd<float> colDiv = driver.AttachComponent<ValueAdd<float>>();
                colDiv.A.TrySet(time);
                colDiv.B.TrySet(offsetSource);

                mid.Hue.TrySet(colDiv);
            }
            else
            {
                mid.Hue.TrySet(time);
            }
            ValueFieldDrive<colorX> hueFieldDrive = driver.AttachComponent<ValueFieldDrive<colorX>>();
            hueFieldDrive.Value.TrySet(mid);
            hueFieldDrive.TrySetRootTarget(DesiredField.Value);

            ValueDiv <colorX> Div = driver.AttachComponent<ValueDiv<colorX>>();
            ValueMul<colorX> Mul = driver.AttachComponent<ValueMul<colorX>>();

            Div.A.TrySet(InputSource);
            Div.B.TrySet(DefaultSource);

            Mul.A.TrySet(Div);
            Mul.B.TrySet(DesiredSource);

            ValueFieldDrive<colorX> Driver = driver.AttachComponent<ValueFieldDrive<colorX>>();
            Driver.Value.TrySet(Mul);
            Driver.TrySetRootTarget(Field);

            return DesiredField;
        }
        

    }
}
