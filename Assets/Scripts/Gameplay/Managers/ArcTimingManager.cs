using System.Collections.Generic;
using System.Linq;
using Arcade.Compose;
using Arcade.Compose.UI;
using Arcade.Gameplay.Chart;
using UnityEngine;

namespace Arcade.Gameplay
{
	public class ArcTimingManager : MonoBehaviour
	{
		public static ArcTimingManager Instance { get; private set; }
		private void Awake()
		{
			Instance = this;
		}
		private void Start()
		{
			speedShaderId = Shader.PropertyToID("_Speed");
		}

		private int velocity = 30;
		public float BaseBpm = 100;
		public Transform BeatlineLayer;
		public GameObject BeatlinePrefab;

		// Note: This should be ordered!
		[HideInInspector]
		private List<ArcTiming> timings = new List<ArcTiming>();
		public SpriteRenderer TrackRenderer;

		private List<float> beatlineTimings = new List<float>();
		private List<SpriteRenderer> beatLineInstances = new List<SpriteRenderer>();
		private float earliestRenderTime = 0;
		private float latestRenderTime = 0;
		private int speedShaderId = 0;
		public float CurrentSpeed { get; set; }
		public List<ArcTiming> Timings { get => timings; }
		public int Velocity
		{
			get => velocity; set{
				velocity = value;
				ArcArcManager.Instance.Rebuild();
				AdeSpeedSlider.Instance.UpdateVelocity(value);
			}
		}

		private void Update()
		{
			if (Timings == null) return;
			if (Timings.Count == 0) return;
			UpdateChartSpeedStatus();
			UpdateRenderRange();
			UpdateBeatline();
			UpdateTrackSpeed();
		}

		public void Clean()
		{
			CurrentSpeed = 0;
			Timings.Clear();
			TrackRenderer.sharedMaterial.SetFloat(speedShaderId, 0);
			HideExceededBeatlineInstance(0);
		}
		public void Load(List<ArcTiming> arcTimings)
		{
			// Note: We replaced the inplace sort by sort to another list and reassign
			// just because we do not have stable inplace sort now in dot net
			timings = arcTimings.OrderBy((timing) => timing.Timing).ToList();
			ArcGameplayManager.Instance.Chart.Timings = timings;
			OnTimingChange();
		}
		private void HideExceededBeatlineInstance(int quantity)
		{
			int count = beatLineInstances.Count;
			while (count > quantity)
			{
				beatLineInstances[count - 1].enabled = false;
				count--;
			}
		}
		private SpriteRenderer GetBeatlineInstance(int index)
		{
			while (beatLineInstances.Count < index + 1)
			{
				beatLineInstances.Add(Instantiate(BeatlinePrefab, BeatlineLayer).GetComponent<SpriteRenderer>());
			}
			return beatLineInstances[index];
		}
		public void CalculateBeatlineTimes()
		{
			beatlineTimings.Clear();
			HideExceededBeatlineInstance(0);
			if (Timings.Count == 0)
			{
				return;
			}
			for (int i = 0; i < Timings.Count; ++i)
			{
				if (Timings[i].Bpm == 0 || Timings[i].BeatsPerLine == 0)
				{
					beatlineTimings.Add(Timings[i].Timing);
					continue;
				}
				float nextTiming = i + 1 >= Timings.Count ? ArcGameplayManager.Instance.Length : Timings[i + 1].Timing;
				float segment = (60000 / Mathf.Abs(Timings[i].Bpm) * Timings[i].BeatsPerLine);
				if (segment == 0) continue;
				int n = 0;
				while (true)
				{
					float j = Timings[i].Timing + n * segment;
					if (j >= nextTiming)
					{
						break;
					}
					beatlineTimings.Add(j);
					n++;
				}
			}

			if (Timings[0].Bpm != 0 && Timings[0].BeatsPerLine != 0)
			{
				float t = 0;
				float segment = 60000 / Mathf.Abs(Timings[0].Bpm) * Timings[0].BeatsPerLine;
				int n = 0;
				while (true)
				{
					n++;
					t = -n * segment;
					if (t < -ArcAudioManager.Instance.AudioOffset)
					{
						break;
					}
					beatlineTimings.Insert(0, t);
				}
			}
		}

		public float CalculatePositionByTiming(int timing)
		{
			return CalculatePositionByTimingAndStart(ArcGameplayManager.Instance.Timing, timing);
		}
		public float CalculatePositionByTimingAndStart(int pivotTiming, int targetTiming)
		{
			if (Timings.Count == 0)
			{
				return 0;
			}
			int offset = ArcAudioManager.Instance.AudioOffset;
			bool reversed = pivotTiming > targetTiming;
			int startTiming = (reversed ? targetTiming : pivotTiming) - offset;
			int endTiming = (reversed ? pivotTiming : targetTiming) - offset;
			int startTimingId = Timings.FindLastIndex((timing) => timing.Timing <= startTiming);
			int endTimingId = Timings.FindLastIndex((timing) => timing.Timing <= endTiming);
			if (startTimingId == -1)
			{
				startTimingId = 0;
			}
			if (endTimingId == -1)
			{
				endTimingId = 0;
			}
			float result = 0;
			for (int i = startTimingId; i <= endTimingId; i++)
			{
				int segmentStartTiming = i == startTimingId ? startTiming : Timings[i].Timing;
				int segmentEndTiming = i == endTimingId ? endTiming : Timings[i + 1].Timing;
				result += (segmentEndTiming - segmentStartTiming) * Timings[i].Bpm / BaseBpm * Velocity;
			}
			float newresult = reversed ? -result : result;

			return newresult;
		}

		public int CalculateTimingByPosition(float position)
		{
			if (Timings.Count == 0)
			{
				return 0;
			}
			int currentTiming = ArcGameplayManager.Instance.Timing - ArcAudioManager.Instance.AudioOffset;
			if (position < 0)
			{
				return currentTiming;
			}
			int currentTimingId = Timings.FindLastIndex((timing) => timing.Timing <= currentTiming);
			int allEndTime = ArcGameplayManager.Instance.Length - ArcAudioManager.Instance.AudioOffset;
			float positionRemain = position;
			for (int i = currentTimingId; i < Timings.Count; i++)
			{
				int startTiming = i == currentTimingId ? currentTiming : Timings[i].Timing;
				int endTiming = i + 1 == Timings.Count ? allEndTime : Timings[i + 1].Timing;
				float bpm = i == -1 ? Timings[0].Bpm : Timings[i].Bpm;
				float delta = (endTiming - startTiming) * bpm / BaseBpm * Velocity;
				if (delta < positionRemain)
				{
					positionRemain -= delta;
					continue;
				}
				if (delta == 0)
				{
					return startTiming;
				}
				return Mathf.RoundToInt(Mathf.Lerp(startTiming, endTiming, positionRemain / delta)) + ArcAudioManager.Instance.AudioOffset;
			}
			return allEndTime + ArcAudioManager.Instance.AudioOffset;
		}


		public float CalculateBpmByTiming(int timing)
		{
			if (Timings.Count == 0)
			{
				return 0;
			}
			return Timings.Last(timingEvent => timingEvent.Timing <= timing).Bpm;
		}

		private void UpdateChartSpeedStatus()
		{
			int offset = ArcAudioManager.Instance.AudioOffset;
			int currentTiming = ArcGameplayManager.Instance.Timing - offset;
			if (Timings.Count == 0)
			{
				CurrentSpeed = 0;
				return;
			}
			int currentTimingId = Timings.FindLastIndex((timing) => timing.Timing <= currentTiming);
			if (currentTimingId == -1)
			{
				currentTimingId = 0;
			}
			CurrentSpeed = Timings[currentTimingId].Bpm / BaseBpm;
		}
		private void UpdateRenderRange()
		{
			int nearPosition = 0;
			int farPosition = 100000;
			if (timings.Count == 0)
			{
				earliestRenderTime = float.NegativeInfinity;
				latestRenderTime = float.PositiveInfinity;
				return;
			}
			int currentTiming = ArcGameplayManager.Instance.Timing - ArcAudioManager.Instance.AudioOffset;
			int currentTimingId = Timings.FindLastIndex((timing) => timing.Timing <= currentTiming);
			float[] TimingPosition = new float[Timings.Count];
			for (int i = currentTimingId; i + 1 < Timings.Count; i++)
			{
				int startTiming = i == currentTimingId ? currentTiming : Timings[i].Timing;
				int endTiming = Timings[i + 1].Timing;
				float startPosition = i == currentTimingId ? 0 : TimingPosition[i];
				float bpm = i == -1 ? Timings[0].Bpm : Timings[i].Bpm;
				TimingPosition[i + 1] = startPosition + (endTiming - startTiming) * bpm / BaseBpm * Velocity;
			}
			for (int i = currentTimingId; i >= 0; i--)
			{
				int startTiming = i == currentTimingId ? currentTiming : Timings[i + 1].Timing;
				int endTiming = Timings[i].Timing;
				float startPosition = i == currentTimingId ? 0 : TimingPosition[i + 1];
				TimingPosition[i] = startPosition + (endTiming - startTiming) * Timings[i].Bpm / BaseBpm * Velocity;
			}
			earliestRenderTime = float.PositiveInfinity;
			latestRenderTime = float.NegativeInfinity;
			int allBeginTime = -ArcAudioManager.Instance.AudioOffset;
			float allBeginPosition = TimingPosition[0] + (allBeginTime - Timings[0].Timing) * Timings[0].Bpm / BaseBpm * Velocity;
			int allEndTime = ArcGameplayManager.Instance.Length - ArcAudioManager.Instance.AudioOffset;
			float allEndPosition = TimingPosition[Timings.Count - 1] + (allEndTime - Timings[Timings.Count - 1].Timing) * Timings[Timings.Count - 1].Bpm / BaseBpm * Velocity;

			for (int i = -1; i < Timings.Count; i++)
			{
				int startTime = i == -1 ? allBeginTime : Timings[i].Timing;
				int finishTime = i + 1 == Timings.Count ? allEndTime : Timings[i + 1].Timing;
				float startPosition = i == -1 ? allBeginPosition : TimingPosition[i];
				float finishPosition = i + 1 == Timings.Count ? allEndPosition : TimingPosition[i + 1];
				if (finishTime < startTime)
				{
					continue;
				}
				if (startPosition > farPosition && finishPosition > farPosition)
				{
					continue;
				}
				if (startPosition < nearPosition && finishPosition < nearPosition)
				{
					continue;
				}
				float nearTime = Mathf.Lerp(startTime, finishTime, Mathf.InverseLerp(startPosition, finishPosition, nearPosition));
				float farTime = Mathf.Lerp(startTime, finishTime, Mathf.InverseLerp(startPosition, finishPosition, farPosition));
				earliestRenderTime = Mathf.Min(earliestRenderTime, nearTime, farTime);
				latestRenderTime = Mathf.Max(latestRenderTime, nearTime, farTime);
			}
			earliestRenderTime += ArcAudioManager.Instance.AudioOffset;
			latestRenderTime += ArcAudioManager.Instance.AudioOffset;
		}
		private void UpdateBeatline()
		{
			int index = 0;
			int offset = ArcAudioManager.Instance.AudioOffset;
			foreach (float t in beatlineTimings)
			{
				if (!ShouldTryRender((int)(t + offset), 0))
				{
					continue;
				}
				float pos = CalculatePositionByTiming((int)(t + offset));
				if (pos > 100000 || pos < -10000)
				{
					continue;
				}
				SpriteRenderer s = GetBeatlineInstance(index);
				s.enabled = true;
				float z = pos / 1000f;
				s.transform.localPosition = new Vector3(0, 0, -z);
				s.transform.localScale = new Vector3(1700, 20 + z);
				index++;
			}
			HideExceededBeatlineInstance(index);
		}
		private void UpdateTrackSpeed()
		{
			TrackRenderer.sharedMaterial.SetFloat(speedShaderId, ArcGameplayManager.Instance.IsPlaying ? CurrentSpeed : 0);
		}

		public void Add(ArcTiming newTiming)
		{
			timings.Add(newTiming);
			timings = Timings.OrderBy((timing) => timing.Timing).ToList();
			ArcGameplayManager.Instance.Chart.Timings = timings;
			OnTimingChange();
		}
		public void Remove(ArcTiming timing)
		{
			timings.Remove(timing);
			OnTimingChange();
		}
		public void OnTimingChange()
		{
			CalculateBeatlineTimes();
			AdeGridManager.Instance.ReBuildBeatline();
		}
		// Note: this is a function used to optimize rendering by avoid not needed position calculation
		// Invoker should manually check position again after this check passed
		public bool ShouldTryRender(int timing, int delay = 120)
		{
			if (timing + delay >= earliestRenderTime && timing <= latestRenderTime)
			{
				return true;
			}
			return false;
		}
	}
}
