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


        private static List<Sync<colorX>> RightNearColors = new List<Sync<colorX>>();
        private static List<Sync<colorX>> RightFarColors = new List<Sync<colorX>>();
        private static List<Sync<colorX>> LeftNearColors = new List<Sync<colorX>>();
        private static List<Sync<colorX>> LeftFarColors = new List<Sync<colorX>>();
        private static List<Sync<string>> RightNearVars = new List<Sync<string>>();
        private static List<Sync<string>> RightFarVars = new List<Sync<string>>();
        private static List<Sync<string>> LeftNearVars = new List<Sync<string>>();
        private static List<Sync<string>> LeftFarVars = new List<Sync<string>>();


        public static ModConfiguration config;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);



        public override void OnEngineInit()

        {
            config = GetConfiguration();
            config.Save(true);
            config.OnThisConfigurationChanged += OnConfigurationUpdate;

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

                    var Assets = __instance.Slot.AddSlot("Assets");
                    ValueField<colorX> ColE = Assets.AttachComponent<ValueField<colorX>>();
                    ValueField<colorX> ColS = Assets.AttachComponent<ValueField<colorX>>(); //remove?
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
                    //colorX sc = IsRight ? config.GetValue(RIGHT_NEAR) : config.GetValue(LEFT_NEAR);
                    //colorX ec = IsRight ? config.GetValue(RIGHT_FAR) : config.GetValue(LEFT_FAR);
                    //String sv = IsRight ? config.GetValue(RIGHT_NEAR_VAR) : config.GetValue(LEFT_NEAR_VAR);
                    //String ev = IsRight ? config.GetValue(RIGHT_FAR_VAR) : config.GetValue(LEFT_FAR_VAR);

                    Mesh.StartPointColor.Value = new colorX(.25f, 1f, 1f, 1f);
                    Mesh.EndPointColor.Value = new colorX(.25f, 1f, 1f, 1f);


                    ValueField<colorX> Start = SetUpLogix(Assets, ColS.Value, Mesh.StartPointColor, true, IsRight); //remove ColS?
                    ValueField<colorX> End = SetUpLogix(Assets, ColE.Value, Mesh.EndPointColor, false, IsRight);

                    //DynamicField<colorX> FStart = Start.Slot.AttachComponent<DynamicField<colorX>>();
                   // DynamicField<colorX> FEnd = End.Slot.AttachComponent<DynamicField<colorX>>();

                   // FStart.VariableName.Value = sv;
                   // FEnd.VariableName.Value = ev;

                   // FStart.TargetField.Target = Start.Value;
                   // FEnd.TargetField.Target = End.Value;



                    if (IsRight)
                    {
                        RightNearColors.Add(Start.Value);
                        Start.Disposing += (field) => { RightNearColors.Remove(Start.Value); };
                        RightFarColors.Add(End.Value);
                        End.Disposing += (field) => { RightFarColors.Remove(End.Value); };
                      //  RightNearVars.Add(FStart.VariableName);
                       // Start.Disposing += (field) => { RightNearVars.Remove(FStart.VariableName); };
                       // RightFarVars.Add(FEnd.VariableName);
                       // End.Disposing += (field) => { RightFarVars.Remove(FEnd.VariableName); };
                    }
                    else
                    {
                        LeftNearColors.Add(Start.Value);
                        Start.Disposing += (field) => { LeftNearColors.Remove(Start.Value); };
                        LeftFarColors.Add(End.Value);
                        End.Disposing += (field) => { LeftFarColors.Remove(End.Value); };
                       // LeftNearVars.Add(FStart.VariableName);
                       // Start.Disposing += (field) => { LeftNearVars.Remove(FStart.VariableName); };
                       // LeftFarVars.Add(FEnd.VariableName);
                       // End.Disposing += (field) => { LeftFarVars.Remove(FEnd.VariableName); };
                    }

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
            // Field <= (Input / Default) * Desired
            // This should be LogiX so the color is actually networked.

            Slot driver = root.AddSlot(IsStart ? "Start" : "End");


            WorldTime2Float time = driver.AttachComponent<WorldTime2Float>();          
            ColorXHue mid = driver.AttachComponent<ColorXHue>();
            // mid.TryConnectInput(time.Time.WorldTimeFloat);
            // mid.Hue.Value = time.Time.WorldTimeFloat;
            mid.Hue.TrySet(time);
            ValueFieldDrive<colorX> DesiredDriver = driver.AttachComponent<ValueFieldDrive<colorX>>();
            DesiredDriver.Value.TrySet(mid.Hue);
            //ValueSource<colorX> DesiredSource = driver.AttachComponent<ValueSource<colorX>>();
            //DesiredSource.TrySetRootSource(mid);


            ValueSource<colorX> InputSource = driver.AttachComponent<ValueSource<colorX>>();
            InputSource.TrySetRootSource(Input);


            ValueField<colorX> Default = driver.AttachComponent<ValueField<colorX>>();       //StaticValue 
            Default.Value.Value = new colorX(.25f, 1f, 1f, 1f);           
            ValueSource<colorX> DefaultSource = driver.AttachComponent<ValueSource<colorX>>();//StaticValue 
            DefaultSource.TrySetRootSource(Default.Value);                 


            ValueField<colorX> DesiredField = driver.AttachComponent<ValueField<colorX>>();
            DesiredField.Value.Value = new colorX(.25f, 1f, 1f, 1f); 

            ValueSource<colorX> DesiredSource = driver.AttachComponent<ValueSource<colorX>>();
            DesiredSource.TrySetRootSource(DesiredField.Value); 
           

            ValueDiv<colorX> Div = driver.AttachComponent<ValueDiv<colorX>>();
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
