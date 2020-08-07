using UnityEngine;

namespace BDArmory.FX
{
	public class DecalEmitterScript : MonoBehaviour
	{
		//public static float _maxCombineDistance = 0.6f;

		public float lifeTime = 20;

		private GameObject _destroyer;

		private float _destroyTimerStart;

		private float _highestEnergy;

		public void Start()
		{
			foreach (var pe in gameObject.GetComponentsInChildren<KSPParticleEmitter>())
			{
				pe.force = FlightGlobals.getGeeForceAtPosition(transform.position);
				if (!(pe.maxEnergy > _highestEnergy)) continue;
				_destroyer = pe.gameObject;
				_highestEnergy = pe.maxEnergy;
				EffectBehaviour.AddParticleEmitter(pe);
			}
		}

		public void FixedUpdate()
		{
			if (_destroyTimerStart != 0 && Time.time - _destroyTimerStart > _highestEnergy)
			{
				Destroy(gameObject);
			}

			foreach (var pe in gameObject.GetComponentsInChildren<KSPParticleEmitter>())
			{
				//var shrinkRate = pe.gameObject.name.Contains("smoke") ? shrinkRateSmoke : shrinkRateFlame;
				//pe.maxSize = Mathf.MoveTowards(pe.maxSize, 0, shrinkRate * Time.fixedDeltaTime);
				//pe.minSize = Mathf.MoveTowards(pe.minSize, 0, shrinkRate * Time.fixedDeltaTime);

				lifeTime -= Time.fixedDeltaTime;

				if (lifeTime < 0 && pe.gameObject == _destroyer && _destroyTimerStart == 0)
				{
					_destroyTimerStart = Time.time;
				}
			}
		}

		private void OnDestroy()
		{
			foreach (var pe in gameObject.GetComponentsInChildren<KSPParticleEmitter>())
			{
				EffectBehaviour.RemoveParticleEmitter(pe);
			}

		}
	}
}
