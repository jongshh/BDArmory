using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
	public class ModuleDrainFuel : PartModule
	{
		public float drainRate;
		public float drainDuration = 20;

		public override void OnStart(StartState state)
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				part.force_activate();
			}
			base.OnStart(state);
		}

		public void Update()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				drainDuration -= Time.fixedDeltaTime;
				if (drainDuration > 0)
				{
					PartResource fuel = part.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
					if (fuel != null)
					{
						if (fuel.amount >= 0)
						{
							part.RequestResource("LiquidFuel", drainRate * Time.fixedDeltaTime);
						}
					}
					PartResource ox = part.Resources.Where(pr => pr.resourceName == "Oxidizer").FirstOrDefault();
					if (ox != null)
					{
						if (ox.amount >= 0)
						{
							part.RequestResource("Oxidizer", drainRate * Time.fixedDeltaTime);
						}
					}
				}
				else
				{
					foreach (var pe in part.GetComponentsInChildren<KSPParticleEmitter>())
					{
						EffectBehaviour.RemoveParticleEmitter(pe);
					}
					part.RemoveModule(this);
				}
			}
		}
	}
}
