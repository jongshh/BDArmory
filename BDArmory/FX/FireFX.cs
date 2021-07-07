using System;
using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using System.Linq;
using UnityEngine;

namespace BDArmory.FX
{
    class FireFX : MonoBehaviour
    {
        Part parentPart;
        public static ObjectPool CreateFireFXPool(string modelPath)
        {
            var template = GameDatabase.Instance.GetModel(modelPath);
            var decal = template.AddComponent<FireFX>();
            template.SetActive(false);
            return ObjectPool.CreateObjectPool(template, 10, true, true);
        }

        private float disableTime = -1;
        private float enginerestartTime = -1;
        private float _highestEnergy = 1;
        public float burnTime = -1;
        private float burnScale = -1;
        private float startTime;
        public bool hasFuel = true;
        public float burnRate = 1;
        private float tntMassEquivilent = 0;
        public bool surfaceFire = false;
        float ScoreAccumulator = 0;
        public string SourceVessel;
        private string explModelPath = "BDArmory/Models/explosion/explosion";
        private string explSoundPath = "BDArmory/Sounds/explode1";

        PartResource fuel;
        PartResource solid;
        PartResource ox;
        PartResource ec;
        PartResource mp;

        private KerbalSeat Seat;
        ModuleEngines engine;
        bool lookedForEngine = false;

        KSPParticleEmitter[] pEmitters;
        void OnEnable()
        {
            if (parentPart == null)
            {
                gameObject.SetActive(false);
                return;
            }
            hasFuel = true;
            startTime = Time.time;
            engine = parentPart.FindModuleImplementing<ModuleEngines>();
            var leak = parentPart.FindModuleImplementing<ModuleDrainFuel>();
            if (leak != null)
            {
                leak.drainDuration = 0;
            }
            foreach (var existingLeakFX in parentPart.GetComponentsInChildren<FuelLeakFX>())
            {
                existingLeakFX.lifeTime = 0; //kill leak FX
            }

            BDArmorySetup.numberOfParticleEmitters++;
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

            using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    pe.Current.emit = true;
                    _highestEnergy = pe.Current.maxEnergy;
                    EffectBehaviour.AddParticleEmitter(pe.Current);
                }

            Seat = null;
            if (parentPart.parent != null)
            {
                var kerbalSeats = parentPart.parent.Modules.OfType<KerbalSeat>();
                if (kerbalSeats.Count() > 0)
                    Seat = kerbalSeats.First();
            }
            if (parentPart.protoModuleCrew.Count > 0) //crew can extingusih fire
            {
                burnTime = 10;
            }
            if (parentPart.parent != null && parentPart.parent.protoModuleCrew.Count > 0 || (Seat != null && Seat.Occupant != null))
            {
                burnTime = 20; //though adjacent parts will take longer to get to and extingusih
            }
            if (!surfaceFire)
            {
                if (parentPart.GetComponent<ModuleSelfSealingTank>() != null)
                {
                    ModuleSelfSealingTank FBX;
                    FBX = parentPart.GetComponent<ModuleSelfSealingTank>();
                    if (FBX.FireBottles > 0)
                    {
                        FBX.FireBottles -= 1;
                        if (engine != null && engine.EngineIgnited && engine.allowRestart)
                        {
                            engine.Shutdown();
                            enginerestartTime = Time.time;
                        }
                        burnTime = 10;
                        Misc.Misc.RefreshAssociatedWindows(parentPart);
                        Debug.Log("[FireFX] firebottles remianing in " + parentPart.name + ": " + FBX.FireBottles);
                    }
                    else
                    {
                        if (engine != null && engine.EngineIgnited && engine.allowRestart)
                        {
                            if (parentPart.vessel.verticalSpeed < 30) //not diving/trying to climb. With the vessel registry, could also grab AI state to add a !evading check
                            {
                                engine.Shutdown();
                                enginerestartTime = Time.time + 10;
                                burnTime = 20;
                            }
                            //though if it is diving, then there isn't a second call to cycle engines. Add an Ienumerator to check once every couple sec?
                        }
                    }
                }
            }
        }

        void onDisable()
        {
            BDArmorySetup.numberOfParticleEmitters--;
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy || !HighLogic.LoadedSceneIsFlight || BDArmorySetup.GameIsPaused)
            {
                return;
            }
            transform.rotation = Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            fuel = parentPart.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
            if (disableTime < 0) //only have fire do it's stuff while burning and not during FX timeout
            {
                if (!surfaceFire) //is fire inside tank, or an incendiary substance on the part's surface?
                {
                    if (!lookedForEngine)
                    {
                        engine = parentPart.FindModuleImplementing<ModuleEngines>();
                        lookedForEngine = true; //have this only called once, not once per update tick
                    }
                    if (engine != null)
                    {
                        if (engine.throttleLocked && !engine.allowShutdown) //likely a SRB
                        {
                            if (parentPart.RequestResource("SolidFuel", (double)(burnRate * TimeWarp.deltaTime)) <= 0)
                            {
                                hasFuel = false;
                            }
                            solid = parentPart.Resources.Where(pr => pr.resourceName == "SolidFuel").FirstOrDefault();
                            if (solid != null)
                            {
                                if (solid.amount < solid.maxAmount * 0.66f)
                                {
                                    engine.Activate(); //SRB lights from unintended ignition source
                                }
                                if (solid.amount < solid.maxAmount * 0.15f)
                                {
                                    tntMassEquivilent += Mathf.Clamp((float)solid.amount, ((float)solid.maxAmount * 0.05f), ((float)solid.maxAmount * 0.2f));
                                    Detonate(); //casing's full of holes and SRB fuel's burnt to the point it can easily start venting through those holes
                                }
                            }
                        }
                        else
                        {
                            if (engine.EngineIgnited)
                            {
                                if (parentPart.RequestResource("LiquidFuel", (double)(burnRate * TimeWarp.deltaTime)) <= 0)
                                {
                                    hasFuel = false;
                                }
                            }
                            else
                            {
                                hasFuel = false;
                            }
                        }
                    }
                    else
                    {
                        if (fuel != null)
                        {
                            if (parentPart.vessel.atmDensity < 0.05 && ox == null)
                            {
                                hasFuel = false;
                            }
                            else
                            {
                                if (fuel.amount > (fuel.maxAmount * 0.15f) || (fuel.amount > 0 && fuel.amount < (fuel.maxAmount * 0.10f)))
                                {
                                    fuel.amount -= (burnRate * Mathf.Clamp((float)((1 - (fuel.amount / fuel.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 4 * BDArmorySettings.BD_TANK_LEAK_RATE) * TimeWarp.deltaTime);
                                    burnScale = Mathf.Clamp((float)((1 - (fuel.amount / fuel.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 2 * BDArmorySettings.BD_TANK_LEAK_RATE);
                                }
                                else if (fuel.amount < (fuel.maxAmount * 0.15f) && fuel.amount > (fuel.maxAmount * 0.10f))
                                {
                                    Detonate();
                                }
                                else
                                {
                                    hasFuel = false;
                                }
                            }
                        }
                        ox = parentPart.Resources.Where(pr => pr.resourceName == "Oxidizer").FirstOrDefault();
                        if (ox != null)
                        {
                            if (ox.amount > 0)
                            {
                                ox.amount -= (burnRate * Mathf.Clamp((float)((1 - (ox.amount / ox.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 4 * BDArmorySettings.BD_TANK_LEAK_RATE) * TimeWarp.deltaTime);
                            }
                            else
                            {
                                hasFuel = false;
                            }
                        }
                        mp = parentPart.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                        if (mp != null)
                        {
                            if (mp.amount > (mp.maxAmount * 0.15f) || (mp.amount > 0 && mp.amount < (mp.maxAmount * 0.10f)))
                            {
                                mp.amount -= (burnRate * Mathf.Clamp((float)((1 - (mp.amount / mp.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 4 * BDArmorySettings.BD_TANK_LEAK_RATE) * TimeWarp.deltaTime);
                                if (burnScale < 0)
                                {
                                    burnScale = Mathf.Clamp((float)((1 - (mp.amount / mp.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 2 * BDArmorySettings.BD_TANK_LEAK_RATE);
                                }
                            }
                            else if (mp.amount < (mp.maxAmount * 0.15f) && mp.amount > (mp.maxAmount * 0.10f))
                            {
                                Detonate();
                            }
                            else
                            {
                                hasFuel = false;
                            }
                        }
                        ec = parentPart.Resources.Where(pr => pr.resourceName == "ElectricCharge").FirstOrDefault();
                        if (ec != null)
                        {
                            if (parentPart.vessel.atmDensity < 0.05)
                            {
                                hasFuel = false;
                            }
                            else
                            {
                                if (ec.amount > 0)
                                {
                                    ec.amount -= (burnRate * TimeWarp.deltaTime);
                                    Mathf.Clamp((float)ec.amount, 0, Mathf.Infinity);
                                    if (burnScale < 0)
                                    {
                                        burnScale = 1;
                                    }
                                }
                                if ((Time.time - startTime > 30) && engine == null)
                                {
                                    Detonate();
                                }
                            }
                        }
                    }
                }
                if (BDArmorySettings.BD_FIRE_HEATDMG)
                {
                    if (parentPart.temperature < 1300)
                    {
                        if (fuel != null)
                        {
                            parentPart.temperature += burnRate * Mathf.Clamp((float)((1 - (fuel.amount / fuel.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 4 * BDArmorySettings.BD_TANK_LEAK_RATE) * Time.deltaTime;
                        }
                        else if (mp != null)
                        {
                            parentPart.temperature += burnRate * Mathf.Clamp((float)((1 - (mp.amount / mp.maxAmount)) * 4), 0.1f * BDArmorySettings.BD_TANK_LEAK_RATE, 4 * BDArmorySettings.BD_TANK_LEAK_RATE) * Time.deltaTime;
                        }
                        else if (ec != null || ox != null)
                        {
                            parentPart.temperature += burnRate * BDArmorySettings.BD_FIRE_DAMAGE * Time.deltaTime;
                        }
                    }
                }
                if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_FIRE_DOT)
                {
                    if (BDArmorySettings.BD_FIRE_HEATDMG)
                    {
                        if (parentPart.temperature > 1000)
                        {
                            parentPart.AddDamage(BDArmorySettings.BD_FIRE_DAMAGE * Time.deltaTime);
                        }
                    }
                    else
                    {
                        parentPart.AddDamage(BDArmorySettings.BD_FIRE_DAMAGE * Time.deltaTime);
                    }
                    ////////////////////////////////////////////////

                    ScoreAccumulator = 0;
                    var aName = SourceVessel;
                    var tName = parentPart.vessel.GetName();

                    if (aName != null && tName != null && aName != tName && BDACompetitionMode.Instance.Scores.ContainsKey(aName) && BDACompetitionMode.Instance.Scores.ContainsKey(tName))
                    {
                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                        {
                            BDAScoreService.Instance.TrackDamage(aName, tName, BDArmorySettings.BD_FIRE_DAMAGE);
                        }
                        // Track damage. Moving this here to properly track damage per tick
                        var tData = BDACompetitionMode.Instance.Scores[tName];
                        if (tData.damageFromBullets.ContainsKey(aName))
                            tData.damageFromBullets[aName] += BDArmorySettings.BD_FIRE_DAMAGE;
                        else
                            tData.damageFromBullets.Add(aName, BDArmorySettings.BD_FIRE_DAMAGE);

                        if (ScoreAccumulator >= 1) //could be reduced, gaining +1 hit per sec, per fire seems high
                        {
                            var aData = BDACompetitionMode.Instance.Scores[aName];
                            aData.Score += 1;

                            if (parentPart.vessel.GetName() == "Pinata")
                            {
                                aData.PinataHits++;
                            }
                            tData.lastPersonWhoHitMe = aName;
                            tData.lastHitTime = Planetarium.GetUniversalTime();
                            tData.everyoneWhoHitMe.Add(aName);
                            // Track hits
                            if (tData.hitCounts.ContainsKey(aName))
                                ++tData.hitCounts[aName];
                            else
                                tData.hitCounts.Add(aName, 1);
                        }
                    }
                    else
                    {
                        ScoreAccumulator += 1 * Time.deltaTime;
                    }
                }
            }
            if ((!hasFuel && disableTime < 0 && burnTime < 0) || (burnTime > 0 && disableTime < 0 && Time.time - startTime > burnTime))
            {
                disableTime = Time.time; //grab time when emission stops
                foreach (var pe in pEmitters)
                    if (pe != null)
                        pe.emit = false;
            }
            else
            {
                foreach (var pe in pEmitters)
                {
                    pe.maxSize = burnScale;
                    pe.minSize = burnScale * 1.2f;
                }
            }
            if (disableTime > 0 && Time.time - disableTime > _highestEnergy) //wait until last emitted particle has finished
            {
                gameObject.SetActive(false);
            }
            if (engine != null && enginerestartTime > 0 && Time.time - 10 > enginerestartTime)
            {
                engine.Activate();
                enginerestartTime = -1;
            }
            ////////////////////////////////////////////

        }

        void Detonate()
        {
            if (!parentPart.partName.Contains("exploding"))
            {
                bool excessFuel = false;
                parentPart.partName += "exploding";
                PartResource fuel = parentPart.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
                PartResource ox = parentPart.Resources.Where(pr => pr.resourceName == "Oxidizer").FirstOrDefault();
                if (fuel != null)
                {
                    tntMassEquivilent += (Mathf.Clamp((float)fuel.amount, ((float)fuel.maxAmount * 0.05f), ((float)fuel.maxAmount * 0.2f)) / 2);
                    if (fuel != null && ox != null)
                    {
                        tntMassEquivilent += (Mathf.Clamp((float)ox.amount, ((float)ox.maxAmount * 0.1f), ((float)ox.maxAmount * 0.3f)) / 2);
                        tntMassEquivilent *= 1.3f;
                    }
                    if (fuel.amount > fuel.maxAmount * 0.3f)
                    {
                        excessFuel = true;
                    }
                }
                PartResource mp = parentPart.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                if (mp != null)
                {
                    tntMassEquivilent += (Mathf.Clamp((float)mp.amount, ((float)mp.maxAmount * 0.1f), ((float)mp.maxAmount * 0.3f)) / 3);
                    if (mp.amount > mp.maxAmount * 0.3f)
                    {
                        excessFuel = true;
                    }
                }
                PartResource ec = parentPart.Resources.Where(pr => pr.resourceName == "ElectricCharge").FirstOrDefault();
                if (ec != null)
                {
                    tntMassEquivilent += ((float)ec.maxAmount / 5000); //fix for cockpit batteries weighing a tonne+
                    ec.maxAmount = 0;
                    ec.isVisible = false;
                    parentPart.RemoveResource(ec);//destroy battery. not calling part.destroy, since some batteries in cockpits.
                    Misc.Misc.RefreshAssociatedWindows(parentPart);
                }
                tntMassEquivilent *= BDArmorySettings.BD_AMMO_DMG_MULT;
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory.FireFX] Fuel Explosion in " + this.parentPart.name + ", TNT mass equivilent " + tntMassEquivilent);
                }
                if (excessFuel)
                {
                    float blastRadius = BlastPhysicsUtils.CalculateBlastRange(tntMassEquivilent);
                    using (var blastHits = Physics.OverlapSphere(parentPart.transform.position, blastRadius, 9076737).AsEnumerable().GetEnumerator())
                    {
                        while (blastHits.MoveNext())
                        {
                            if (blastHits.Current == null) continue;
                            try
                            {
                                Part partHit = blastHits.Current.GetComponentInParent<Part>();
                                if (partHit == null) continue;
                                if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                                if (partHit != null && partHit.mass > 0)
                                {
                                    Rigidbody rb = partHit.Rigidbody;
                                    Vector3 distToG0 = parentPart.transform.position - partHit.transform.position;

                                    Ray LoSRay = new Ray(parentPart.transform.position, partHit.transform.position - parentPart.transform.position);
                                    RaycastHit hit;
                                    if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737))
                                    {
                                        KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                        Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                                        if (p == partHit)
                                        {
                                            if (rb == null) return;
                                            BulletHitFX.AttachFire(hit, p, 1, SourceVessel, BDArmorySettings.WEAPON_FX_DURATION * (1 - (distToG0.magnitude / blastRadius)));
                                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                            {
                                                Debug.Log("[BDArmory.FireFX] " + this.parentPart.name + " hit by burning fuel");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("[BDArmory.FireFX]: Exception thrown in Detonate: " + e.Message + "\n" + e.StackTrace);
                            }
                        }
                    }
                }
                ExplosionFx.CreateExplosion(parentPart.transform.position, tntMassEquivilent, explModelPath, explSoundPath, ExplosionSourceType.Bullet, 0, null, parentPart.vessel != null ? parentPart.vessel.name : null, null);
                // needs to be Explosiontype Bullet since missile only returns Module MissileLauncher
                gameObject.SetActive(false);
            }
        }

        public void AttachAt(Part hitPart, RaycastHit hit, Vector3 offset, string sourcevessel, float burnTime = -1)
        {
            parentPart = hitPart;
            transform.SetParent(hitPart.transform);
            transform.position = hit.point + offset;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            parentPart.OnJustAboutToDie += OnParentDestroy;
            parentPart.OnJustAboutToBeDestroyed += OnParentDestroy;
            SourceVessel = sourcevessel;
            gameObject.SetActive(true);
        }

        public void OnParentDestroy()
        {
            if (parentPart)
            {
                parentPart.OnJustAboutToDie -= OnParentDestroy;
                parentPart.OnJustAboutToBeDestroyed -= OnParentDestroy;
                if (gameObject.activeInHierarchy)
                    Detonate();
                parentPart = null;
                transform.parent = null;
                gameObject.SetActive(false);
            }
        }

        public void OnDestroy()
        {
            OnParentDestroy();
        }
    }
}
