using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcade.Aff;
using UnityEngine;

namespace Arcade.Gameplay.Chart
{
	public enum ChartSortMode
	{
		Timing,
		Type
	}
	public class ArcChart
	{
		public int AudioOffset;
		public List<ArcTap> Taps = new List<ArcTap>();
		public List<ArcHold> Holds = new List<ArcHold>();
		public List<ArcTiming> Timings = new List<ArcTiming>();
		public List<ArcArc> Arcs = new List<ArcArc>();
		public List<ArcCamera> Cameras = new List<ArcCamera>();
		public List<ArcSceneControl> SceneControl = new List<ArcSceneControl>();
		public int LastEventTiming = 0;

		public ArcChart(RawAffChart raw){
			AudioOffset = raw.AudioOffset;
			foreach (var item in raw.items){
				addRawItem(item);
			}
		}

		void addRawItem(IRawAffItem item){
			if(item is RawAffTiming){
				Timings.Add(new ArcTiming(item as RawAffTiming));
			}else if(item is RawAffTap){
				Taps.Add(new ArcTap(item as RawAffTap));
			}else if(item is RawAffHold){
				Holds.Add(new ArcHold(item as RawAffHold));
			}else if(item is RawAffArc){
				Arcs.Add(new ArcArc(item as RawAffArc));
			}else if(item is RawAffCamera){
				Cameras.Add(new ArcCamera(item as RawAffCamera));
			}else if(item is RawAffSceneControl){
				SceneControl.Add(new ArcSceneControl(item as RawAffSceneControl));
			}
		}

		public void Serialize(Stream stream, ChartSortMode mode = ChartSortMode.Timing)
		{
			List<ArcEvent> events = new List<ArcEvent>();
			events.AddRange(Timings);
			events.AddRange(Taps);
			events.AddRange(Holds);
			events.AddRange(Arcs);
			events.AddRange(Cameras);
			events.AddRange(SceneControl);
			switch (mode)
			{
				case ChartSortMode.Timing:
					events = events.OrderBy(arcEvent => arcEvent.Timing)
						.ThenBy(arcEvent => (arcEvent is ArcTiming ? 1 : arcEvent is ArcTap ? 2 : arcEvent is ArcHold ? 3 : arcEvent is ArcArc ? 4 : 5))
						.ToList();
					break;
				case ChartSortMode.Type:
					events = events.OrderBy(arcEvent => (arcEvent is ArcTiming ? 1 : arcEvent is ArcTap ? 2 : arcEvent is ArcHold ? 3 : arcEvent is ArcArc ? 4 : 5))
						.ThenBy(arcEvent => arcEvent.Timing)
						.ToList();
					break;
			}
			RawAffChart raw=new RawAffChart();
			raw.AudioOffset=ArcAudioManager.Instance.AudioOffset;
			foreach (var e in events)
			{
				if(e is IIntoRawItem){
					raw.items.Add((e as IIntoRawItem).IntoRawItem());
				}
			}
			ArcaeaFileFormat.DumpToStream(stream,raw);
		}
		public static ArcLineType ToArcLineType(string type)
		{
			switch (type)
			{
				case "b": return ArcLineType.B;
				case "s": return ArcLineType.S;
				case "si": return ArcLineType.Si;
				case "so": return ArcLineType.So;
				case "sisi": return ArcLineType.SiSi;
				case "siso": return ArcLineType.SiSo;
				case "sosi": return ArcLineType.SoSi;
				case "soso": return ArcLineType.SoSo;
				default: return ArcLineType.S;
			}
		}
		public static string ToLineTypeString(ArcLineType type)
		{
			switch (type)
			{
				case ArcLineType.B: return "b";
				case ArcLineType.S: return "s";
				case ArcLineType.Si: return "si";
				case ArcLineType.SiSi: return "sisi";
				case ArcLineType.SiSo: return "siso";
				case ArcLineType.So: return "so";
				case ArcLineType.SoSi: return "sosi";
				case ArcLineType.SoSo: return "soso";
				default: return "s";
			}
		}
		public static CameraEaseType ToCameraType(string type)
		{
			switch (type)
			{
				case "l": return CameraEaseType.L;
				case "reset": return CameraEaseType.Reset;
				case "qi": return CameraEaseType.Qi;
				case "qo": return CameraEaseType.Qo;
				case "s": return CameraEaseType.S;
				default: return CameraEaseType.Reset;
			}
		}
		public static string ToCameraTypeString(CameraEaseType type)
		{
			switch (type)
			{
				case CameraEaseType.L: return "l";
				case CameraEaseType.Reset: return "reset";
				case CameraEaseType.Qi: return "qi";
				case CameraEaseType.Qo: return "qo";
				case CameraEaseType.S: return "s";
				default: return "reset";
			}
		}
	}
	public interface ISelectable
	{
		bool Selected { get; set; }
	}
	public enum ArcLineType
	{
		B,
		S,
		Si,
		So,
		SiSi,
		SiSo,
		SoSi,
		SoSo
	}
	public enum CameraEaseType
	{
		L,
		Qi,
		Qo,
		Reset,
		S
	}
	public enum SceneControlType
	{
		TrackHide,
		TrackShow,
		Unknown,
	}
	public abstract class ArcEvent
	{
		public int Timing;
		public virtual void Assign(ArcEvent newValues)
		{
			Timing = newValues.Timing;
		}
		public abstract ArcEvent Clone();
	}
	public class ArcSceneControl : ArcEvent,IIntoRawItem
	{

		public ArcSceneControl()
		{
		}
		public ArcSceneControl(RawAffSceneControl rawAffSceneControl)
		{
			Timing=rawAffSceneControl.Timing;
			if(rawAffSceneControl.Type=="trackhide"&&rawAffSceneControl.Params.Count==0){
				Type=SceneControlType.TrackHide;
			}else if(rawAffSceneControl.Type=="trackshow"&&rawAffSceneControl.Params.Count==0){
				Type=SceneControlType.TrackShow;
			}
		}
		public IRawAffItem IntoRawItem(){
			var item=new RawAffSceneControl(){
				Timing=Timing,
				Type="unknown",
				Params=new List<IRawAffValue>(),
			};
			if(Type==SceneControlType.TrackHide){
				item.Type="trackhide";
			}else if(Type==SceneControlType.TrackShow){
				item.Type="trackshow";
			}
			return item;
		}
		public SceneControlType Type=SceneControlType.Unknown;

		public override ArcEvent Clone()
		{
			throw new NotImplementedException();
		}
	}
	public abstract class ArcNote : ArcEvent, ISelectable
	{
		// This indicated if the sound effect or tap particle of the note is played
		public bool Judged;
		public float Position;

		protected GameObject instance;
		public virtual GameObject Instance
		{
			get
			{
				return instance;
			}
			set
			{
				if (instance != null) Destroy();
				instance = value;
				transform = instance.transform;
				spriteRenderer = instance.GetComponent<SpriteRenderer>();
				meshRenderer = instance.GetComponent<MeshRenderer>();
			}
		}

		protected bool enable;
		public virtual bool Enable
		{
			get
			{
				return enable;
			}
			set
			{
				if (enable != value)
				{
					enable = value;
					if (spriteRenderer != null) spriteRenderer.enabled = value;
					if (meshRenderer != null) meshRenderer.enabled = value;
				}
			}
		}
		public abstract bool Selected { get; set; }

		public Transform transform;
		public SpriteRenderer spriteRenderer;
		public MeshRenderer meshRenderer;

		public abstract void Instantiate();
		public virtual void Destroy()
		{
			if (instance != null) UnityEngine.Object.Destroy(instance);
			instance = null;
			transform = null;
			spriteRenderer = null;
			meshRenderer = null;
		}
	}
	public abstract class ArcLongNote : ArcNote
	{
		public int EndTiming;
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcLongNote n = newValues as ArcLongNote;
			EndTiming = n.EndTiming;
		}

		// This indicate if long note particle present
		public bool Judging;

		// This two should be replaced by judged
		public bool ShouldPlayAudio;
		public bool AudioPlayed;

		// The times where combo increase
		public List<int> JudgeTimings = new List<int>();

	}
	public class ArcTap : ArcNote,IIntoRawItem
	{
		public int Track;

		private bool selected;
		private float currentAlpha;
		private int highlightShaderId, alphaShaderId;
		private MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
		public Dictionary<ArcArcTap, LineRenderer> ConnectionLines = new Dictionary<ArcArcTap, LineRenderer>();
		private BoxCollider boxCollider;

		public ArcTap()
		{
		}
		public ArcTap(RawAffTap rawAffTap)
		{
			Timing=rawAffTap.Timing;
			Track=rawAffTap.Track;
		}

		public IRawAffItem IntoRawItem()
		{
			return new RawAffTap(){
				Timing=Timing,
				Track=Track,
			};
		}

		public float Alpha
		{
			get
			{
				return currentAlpha;
			}
			set
			{
				if (currentAlpha != value)
				{
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetFloat(alphaShaderId, value);
					spriteRenderer.SetPropertyBlock(propertyBlock);
					foreach (var l in ConnectionLines.Values) l.startColor = l.endColor = new Color(l.endColor.r, l.endColor.g, l.endColor.b, value * 0.8f);
					currentAlpha = value;
				}
			}
		}
		public override bool Enable
		{
			get
			{
				return base.Enable;
			}
			set
			{
				if (enable != value)
				{
					base.Enable = value;
					boxCollider.enabled = value;
					foreach (var l in ConnectionLines.Values) l.enabled = value;
				}
			}
		}
		public override bool Selected
		{
			get
			{
				return selected;
			}
			set
			{
				if (selected != value)
				{
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetInt(highlightShaderId, value ? 1 : 0);
					spriteRenderer.SetPropertyBlock(propertyBlock);
					selected = value;
				}
			}
		}
		public override void Destroy()
		{
			base.Destroy();
			boxCollider = null;
			foreach (var l in ConnectionLines.Values) if (l.gameObject != null) UnityEngine.Object.Destroy(l.gameObject);
			ConnectionLines.Clear();
		}
		public override ArcEvent Clone()
		{
			return new ArcTap()
			{
				Timing = Timing,
				Track = Track
			};
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcTap n = newValues as ArcTap;
			Track = n.Track;
		}
		public override GameObject Instance
		{
			get
			{
				return base.Instance;
			}
			set
			{
				if (instance != null) Destroy();
				base.Instance = value;
				boxCollider = instance.GetComponent<BoxCollider>();
				highlightShaderId = Shader.PropertyToID("_Highlight");
				alphaShaderId = Shader.PropertyToID("_Alpha");
				Enable = false;
			}
		}
		public override void Instantiate()
		{
			Instance = UnityEngine.Object.Instantiate(ArcTapNoteManager.Instance.TapNotePrefab, ArcTapNoteManager.Instance.NoteLayer);
		}
		public void SetupArcTapConnection()
		{
			foreach (var l in ConnectionLines.Values) UnityEngine.Object.Destroy(l.gameObject);
			ConnectionLines.Clear();
			foreach (var arc in ArcArcManager.Instance.Arcs)
			{
				if (arc.ArcTaps == null) continue;
				foreach (var arctap in arc.ArcTaps)
				{
					if (Mathf.Abs(arctap.Timing - Timing) <= 1)
					{
						arctap.SetupArcTapConnection();
					}
				}
			}
		}
	}
	public class ArcHold : ArcLongNote,IIntoRawItem
	{
		public int Track;

		private MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

		public void ReloadSkin()
		{
			defaultSprite = ArcHoldNoteManager.Instance.DefaultSprite;
			highlightSprite = ArcHoldNoteManager.Instance.HighlightSprite;
			spriteRenderer.sprite = highlighted ? highlightSprite : defaultSprite;
		}
		public override GameObject Instance
		{
			get
			{
				return base.Instance;
			}
			set
			{
				if (instance != null) Destroy();
				base.Instance = value;
				fromShaderId = Shader.PropertyToID("_From");
				toShaderId = Shader.PropertyToID("_To");
				alphaShaderId = Shader.PropertyToID("_Alpha");
				highlightShaderId = Shader.PropertyToID("_Highlight");
				defaultSprite = ArcHoldNoteManager.Instance.DefaultSprite;
				highlightSprite = ArcHoldNoteManager.Instance.HighlightSprite;
				boxCollider = instance.GetComponent<BoxCollider>();
				CalculateJudgeTimings();
				Enable = false;
				ReloadSkin();
			}
		}
		public override void Destroy()
		{
			base.Destroy();
			boxCollider = null;
			JudgeTimings.Clear();
		}
		public override ArcEvent Clone()
		{
			return new ArcHold()
			{
				Timing = Timing,
				EndTiming = EndTiming,
				Track = Track
			};
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcHold n = newValues as ArcHold;
			Track = n.Track;
			CalculateJudgeTimings();
		}
		public override void Instantiate()
		{
			Instance = UnityEngine.Object.Instantiate(ArcHoldNoteManager.Instance.HoldNotePrefab, ArcHoldNoteManager.Instance.NoteLayer);
		}

		public void CalculateJudgeTimings()
		{
			JudgeTimings.Clear();
			int u = 0;
			double bpm = ArcTimingManager.Instance.CalculateBpmByTiming(Timing);
			if (bpm <= 0) return;
			double interval = 60000f / bpm / (bpm >= 255 ? 1 : 2);
			int total = (int)((EndTiming - Timing) / interval);
			if ((u ^ 1) >= total)
			{
				JudgeTimings.Add((int)(Timing + (EndTiming - Timing) * 0.5f));
				return;
			}
			int n = u ^ 1;
			int t = Timing;
			while (true)
			{
				t = (int)(Timing + n * interval);
				if (t < EndTiming)
				{
					JudgeTimings.Add(t);
				}
				if (total == ++n) break;
			}
		}
		public override bool Enable
		{
			get
			{
				return base.Enable;
			}
			set
			{
				if (enable != value)
				{
					base.Enable = value;
					boxCollider.enabled = value;
				}
			}
		}
		public float From
		{
			get
			{
				return currentFrom;
			}
			set
			{
				if (currentFrom != value)
				{
					currentFrom = value;
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetFloat(fromShaderId, value);
					spriteRenderer.SetPropertyBlock(propertyBlock);
				}
			}
		}
		public float To
		{
			get
			{
				return currentTo;
			}
			set
			{
				if (currentTo != value)
				{
					currentTo = value;
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetFloat(toShaderId, value);
					spriteRenderer.SetPropertyBlock(propertyBlock);
				}
			}
		}
		public float Alpha
		{
			get
			{
				return currentAlpha;
			}
			set
			{
				if (currentAlpha != value)
				{
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetFloat(alphaShaderId, value);
					spriteRenderer.SetPropertyBlock(propertyBlock);
					currentAlpha = value;
				}
			}
		}
		public override bool Selected
		{
			get
			{
				return selected;
			}
			set
			{
				if (selected != value)
				{
					spriteRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetInt(highlightShaderId, value ? 1 : 0);
					spriteRenderer.SetPropertyBlock(propertyBlock);
					selected = value;
				}
			}
		}
		public bool Highlight
		{
			get
			{
				return highlighted;
			}
			set
			{
				if (highlighted != value)
				{
					highlighted = value;
					spriteRenderer.sprite = value ? highlightSprite : defaultSprite;
				}
			}
		}

		public int FlashCount;
		public BoxCollider boxCollider;

		private bool selected;
		private bool highlighted;
		private int fromShaderId = 0, toShaderId = 0, alphaShaderId = 0, highlightShaderId = 0;
		private float currentFrom = 0, currentTo = 1, currentAlpha = 1;
		private Sprite defaultSprite, highlightSprite;

		public ArcHold()
		{
		}
		public ArcHold(RawAffHold rawAffHold)
		{
			Timing=rawAffHold.Timing;
			EndTiming=rawAffHold.EndTiming;
			Track=rawAffHold.Track;
		}
		public IRawAffItem IntoRawItem(){
			return new RawAffHold(){
				Timing=Timing,
				EndTiming=EndTiming,
				Track=Track,
			};
		}
	}
	public class ArcTiming : ArcEvent,IIntoRawItem
	{
		public float Bpm;
		public float BeatsPerLine;

		public ArcTiming()
		{
		}

		public ArcTiming(RawAffTiming rawAffTiming)
		{
			Timing=rawAffTiming.Timing;
			Bpm=rawAffTiming.Bpm;
			BeatsPerLine=rawAffTiming.BeatsPerLine;
		}
		public IRawAffItem IntoRawItem(){
			return new RawAffTiming(){
				Timing=Timing,
				Bpm=Bpm,
				BeatsPerLine=BeatsPerLine,
			};
		}

		public override ArcEvent Clone()
		{
			return new ArcTiming()
			{
				Timing = Timing,
				Bpm = Bpm,
				BeatsPerLine = BeatsPerLine
			};
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcTiming n = newValues as ArcTiming;
			Bpm = n.Bpm;
			BeatsPerLine = n.BeatsPerLine;
		}
	}
	public class ArcArcTap : ArcNote
	{
		public ArcArc Arc;

		public Transform Model;
		public Transform Shadow;
		public MeshRenderer ModelRenderer;
		public SpriteRenderer ShadowRenderer;
		public MeshCollider ArcTapCollider;
		private MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

		public override bool Enable
		{
			get
			{
				return base.Enable;
			}
			set
			{
				if (enable != value)
				{
					base.Enable = value;
					ModelRenderer.enabled = value;
					ShadowRenderer.enabled = value;
					ArcTapCollider.enabled = value;
				}
			}
		}
		public override GameObject Instance
		{
			get
			{
				return base.Instance;
			}
			set
			{
				if (instance != null) Destroy();
				base.Instance = value;
				ModelRenderer = instance.GetComponentInChildren<MeshRenderer>();
				Model = ModelRenderer.transform;
				ShadowRenderer = instance.GetComponentInChildren<SpriteRenderer>();
				Shadow = ShadowRenderer.transform;
				ModelRenderer.sortingLayerName = "Arc";
				ModelRenderer.sortingOrder = 4;
				alphaShaderId = Shader.PropertyToID("_Alpha");
				highlightShaderId = Shader.PropertyToID("_Highlight");
				ArcTapCollider = instance.GetComponentInChildren<MeshCollider>();
				Enable = false;
			}
		}
		public override void Destroy()
		{
			RemoveArcTapConnection();
			base.Destroy();
			ArcTapCollider = null;
			ModelRenderer = null;
			ShadowRenderer = null;
		}
		public override ArcEvent Clone()
		{
			return new ArcArcTap()
			{
				Timing = Timing
			};
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
		}
		public void Instantiate(ArcArc arc)
		{
			Arc = arc;
			Instance = UnityEngine.Object.Instantiate(ArcArcManager.Instance.ArcTapPrefab, arc.transform);
			ArcTimingManager timingManager = ArcTimingManager.Instance;
			int offset = ArcAudioManager.Instance.AudioOffset;
			float t = 1f * (Timing - arc.Timing) / (arc.EndTiming - arc.Timing);
			LocalPosition = new Vector3(ArcAlgorithm.ArcXToWorld(ArcAlgorithm.X(arc.XStart, arc.XEnd, t, arc.LineType)),
									  ArcAlgorithm.ArcYToWorld(ArcAlgorithm.Y(arc.YStart, arc.YEnd, t, arc.LineType)) - 0.5f,
									  -timingManager.CalculatePositionByTimingAndStart(arc.Timing + offset, Timing + offset) / 1000f - 0.6f);
			SetupArcTapConnection();
		}
		/// <summary>
		/// Please use the overload method.
		/// </summary>
		public override void Instantiate()
		{
			throw new NotImplementedException();
		}

		public void SetupArcTapConnection()
		{
			if (Arc == null || (Arc.EndTiming - Arc.Timing) == 0) return;
			List<ArcTap> taps = ArcTapNoteManager.Instance.Taps;
			ArcTap[] sameTimeTapNotes = taps.Where((s) => Mathf.Abs(s.Timing - Timing) <= 1).ToArray();
			foreach (ArcTap t in sameTimeTapNotes)
			{
				LineRenderer l = UnityEngine.Object.Instantiate(ArcArcManager.Instance.ConnectionPrefab, t.transform).GetComponent<LineRenderer>();
				float p = 1f * (Timing - Arc.Timing) / (Arc.EndTiming - Arc.Timing);
				Vector3 pos = new Vector3(ArcAlgorithm.ArcXToWorld(ArcAlgorithm.X(Arc.XStart, Arc.XEnd, p, Arc.LineType)),
											 ArcAlgorithm.ArcYToWorld(ArcAlgorithm.Y(Arc.YStart, Arc.YEnd, p, Arc.LineType)) - 0.5f)
											 - new Vector3(ArcArcManager.Instance.Lanes[t.Track - 1], 0);
				l.SetPosition(1, new Vector3(pos.x, 0, pos.y));
				l.startColor = l.endColor = ArcArcManager.Instance.ConnectionColor;
				l.startColor = l.endColor = new Color(l.endColor.r, l.endColor.g, l.endColor.b, t.Alpha * 0.8f);
				l.enabled = t.Enable;
				l.transform.localPosition = new Vector3();

				if (t.ConnectionLines.ContainsKey(this))
				{
					UnityEngine.Object.Destroy(t.ConnectionLines[this].gameObject);
					t.ConnectionLines.Remove(this);
				}

				t.ConnectionLines.Add(this, l);
			}
		}
		public void RemoveArcTapConnection()
		{
			List<ArcTap> taps = ArcTapNoteManager.Instance.Taps;
			ArcTap[] sameTimeTapNotes = taps.Where((s) => Mathf.Abs(s.Timing - Timing) <= 1).ToArray();
			foreach (ArcTap t in sameTimeTapNotes)
			{
				if (t.ConnectionLines.ContainsKey(this))
				{
					UnityEngine.Object.Destroy(t.ConnectionLines[this].gameObject);
					t.ConnectionLines.Remove(this);
				}
			}
		}
		public void Relocate()
		{
			ArcTimingManager timingManager = ArcTimingManager.Instance;
			int offset = ArcAudioManager.Instance.AudioOffset;
			float t = 1f * (Timing - Arc.Timing) / (Arc.EndTiming - Arc.Timing);
			LocalPosition = new Vector3(ArcAlgorithm.ArcXToWorld(ArcAlgorithm.X(Arc.XStart, Arc.XEnd, t, Arc.LineType)),
									  ArcAlgorithm.ArcYToWorld(ArcAlgorithm.Y(Arc.YStart, Arc.YEnd, t, Arc.LineType)) - 0.5f,
									  -timingManager.CalculatePositionByTimingAndStart(Arc.Timing + offset, Timing + offset) / 1000f - 0.6f);
			SetupArcTapConnection();
		}

		public bool IsMyself(GameObject gameObject)
		{
			return Model.gameObject.Equals(gameObject);
		}

		public float Alpha
		{
			get
			{
				return currentAlpha;
			}
			set
			{
				if (currentAlpha != value)
				{
					currentAlpha = value;
					ModelRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetFloat(alphaShaderId, value);
					ModelRenderer.SetPropertyBlock(propertyBlock);
					ShadowRenderer.color = new Color(0.49f, 0.49f, 0.49f, 0.7843f * value);
				}
			}
		}
		public Vector3 LocalPosition
		{
			get
			{
				return Model.localPosition;
			}
			set
			{
				Model.localPosition = value;
				Vector3 p = value;
				p.y = 0;
				Shadow.localPosition = p;
			}
		}
		public override bool Selected
		{
			get
			{
				return selected;
			}

			set
			{
				if (selected != value)
				{
					ModelRenderer.GetPropertyBlock(propertyBlock);
					propertyBlock.SetInt(highlightShaderId, value ? 1 : 0);
					ModelRenderer.SetPropertyBlock(propertyBlock);
					selected = value;
				}
			}
		}

		private bool selected;
		private float currentAlpha = 1f;
		private int alphaShaderId = 0, highlightShaderId = 0;

		public ArcArcTap()
		{
		}
		public ArcArcTap(RawAffArctap arctap)
		{
			Timing=arctap.Timing;
		}
	}
	public class ArcArc : ArcLongNote,IIntoRawItem
	{
		public float XStart;
		public float XEnd;
		public ArcLineType LineType;
		public float YStart;
		public float YEnd;
		public int Color;
		public bool IsVoid;
		public List<ArcArcTap> ArcTaps = new List<ArcArcTap>();

		public override ArcEvent Clone()
		{
			ArcArc arc = new ArcArc()
			{
				Timing = Timing,
				EndTiming = EndTiming,
				XStart = XStart,
				XEnd = XEnd,
				LineType = LineType,
				YStart = YStart,
				YEnd = YEnd,
				Color = Color,
				IsVoid = IsVoid,
			};
			foreach (var arctap in ArcTaps) arc.ArcTaps.Add(arctap.Clone() as ArcArcTap);
			return arc;
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcArc n = newValues as ArcArc;
			XStart = n.XStart;
			XEnd = n.XEnd;
			LineType = n.LineType;
			YStart = n.YStart;
			YEnd = n.YEnd;
			Color = n.Color;
			IsVoid = n.IsVoid;
		}

		public override bool Enable
		{
			get
			{
				return base.Enable;
			}
			set
			{
				if (enable != value)
				{
					base.Enable = value;
					arcRenderer.Enable = value;
					if (!value) foreach (ArcArcTap t in ArcTaps) if (t.Instance != null) t.Enable = value;
				}
			}
		}
		public override bool Selected
		{
			get
			{
				return arcRenderer.Selected;
			}
			set
			{
				arcRenderer.Selected = value;
			}
		}
		public override GameObject Instance
		{
			get
			{
				return base.Instance;
			}
			set
			{
				base.Instance = value;
				arcRenderer = instance.GetComponent<ArcArcRenderer>();
				arcRenderer.Arc = this;
				Enable = false;
			}
		}
		public override void Destroy()
		{
			base.Destroy();
			arcRenderer = null;
			ArcGroup.Clear();
			DestroyArcTaps();
		}
		public override void Instantiate()
		{
			Instance = UnityEngine.Object.Instantiate(ArcArcManager.Instance.ArcNotePrefab, ArcArcManager.Instance.ArcLayer);
			InstantiateArcTaps();
		}

		public void CalculateJudgeTimings()
		{
			JudgeTimings.Clear();
			if (IsVoid) return;
			if (EndTiming == Timing) return;
			int u = RenderHead ? 0 : 1;
			double bpm = ArcTimingManager.Instance.CalculateBpmByTiming(Timing);
			if (bpm <= 0) return;
			double interval = 60000f / bpm / (bpm >= 255 ? 1 : 2);
			int total = (int)((EndTiming - Timing) / interval);
			if ((u ^ 1) >= total)
			{
				JudgeTimings.Add((int)(Timing + (EndTiming - Timing) * 0.5f));
				return;
			}
			int n = u ^ 1;
			int t = Timing;
			while (true)
			{
				t = (int)(Timing + n * interval);
				if (t < EndTiming)
				{
					JudgeTimings.Add(t);
				}
				if (total == ++n) break;
			}
		}
		public void Rebuild()
		{
			arcRenderer.Build();
			DestroyArcTaps();
			InstantiateArcTaps();
		}
		public void InstantiateArcTaps()
		{
			foreach (var tap in ArcTaps)
			{
				tap.Instantiate(this);
			}
		}
		public void DestroyArcTaps()
		{
			foreach (var at in ArcTaps)
			{
				at.Destroy();
			}
		}
		public void AddArcTap(ArcArcTap arctap)
		{
			if (arctap.Timing > EndTiming || arctap.Timing < Timing)
			{
				throw new ArgumentOutOfRangeException("ArcTap 时间不在 Arc 范围内");
			}
			ArcTimingManager timingManager = ArcTimingManager.Instance;
			int offset = ArcAudioManager.Instance.AudioOffset;
			arctap.Instantiate(this);
			float t = 1f * (arctap.Timing - Timing) / (EndTiming - Timing);
			arctap.LocalPosition = new Vector3(ArcAlgorithm.ArcXToWorld(ArcAlgorithm.X(XStart, XEnd, t, LineType)),
									  ArcAlgorithm.ArcYToWorld(ArcAlgorithm.Y(YStart, YEnd, t, LineType)) - 0.5f,
									  -timingManager.CalculatePositionByTimingAndStart(Timing + offset, arctap.Timing + offset) / 1000f - 0.6f);
			ArcTaps.Add(arctap);
		}
		public void RemoveArcTap(ArcArcTap arctap)
		{
			arctap.Destroy();
			ArcTaps.Remove(arctap);
		}

		public bool IsMyself(GameObject gameObject)
		{
			return arcRenderer.IsMyself(gameObject);
		}

		public int FlashCount;
		public float EndPosition;
		public bool Flag;
		public bool RenderHead;
		public List<ArcArc> ArcGroup;
		public ArcArcRenderer arcRenderer;

		public ArcArc()
		{
		}
		public ArcArc(RawAffArc rawAffArc)
		{
			Timing=rawAffArc.Timing;
			EndTiming=rawAffArc.EndTiming;
			XStart=rawAffArc.XStart;
			XEnd=rawAffArc.XEnd;
			LineType=rawAffArc.LineType;
			YStart=rawAffArc.YStart;
			YEnd=rawAffArc.YEnd;
			Color=rawAffArc.Color;
			IsVoid=rawAffArc.IsVoid;
			if(rawAffArc.ArcTaps.Count>0){
				IsVoid=true;
				foreach (var arctap in rawAffArc.ArcTaps)
				{
					ArcTaps.Add(new ArcArcTap(arctap));
				}
			}
		}
		public IRawAffItem IntoRawItem(){
			return new RawAffArc(){
				Timing=Timing,
				EndTiming=EndTiming,
				XStart=XStart,
				XEnd=XEnd,
				LineType=LineType,
				YStart=YStart,
				YEnd=YEnd,
				Color=Color,
				IsVoid=IsVoid,
				ArcTaps=ArcTaps.Select((arctap)=>new RawAffArctap(){Timing=arctap.Timing,}).ToList(),
			};
		}

	}
	public class ArcCamera : ArcEvent,IIntoRawItem
	{
		public Vector3 Move, Rotate;
		public CameraEaseType CameraType;
		public int Duration;

		public float Percent;

		public ArcCamera()
		{
		}

		public ArcCamera(RawAffCamera rawAffCamera)
		{
			Timing=rawAffCamera.Timing;
			Move=new Vector3(rawAffCamera.MoveX,rawAffCamera.MoveY,rawAffCamera.MoveZ);
			Rotate=new Vector3(rawAffCamera.RotateX,rawAffCamera.RotateY,rawAffCamera.RotateZ);
			CameraType=rawAffCamera.CameraType;
			Duration=rawAffCamera.Duration;
		}
		public IRawAffItem IntoRawItem(){
			return new RawAffCamera(){
				Timing=Timing,
				MoveX=Move.x,
				MoveY=Move.y,
				MoveZ=Move.z,
				RotateX=Rotate.x,
				RotateY=Rotate.y,
				RotateZ=Rotate.z,
				CameraType=CameraType,
				Duration=Duration,
			};
		}

		public override ArcEvent Clone()
		{
			return new ArcCamera()
			{
				Timing = Timing,
				Duration = Duration,
				CameraType = CameraType,
				Move = Move,
				Percent = Percent,
				Rotate = Rotate
			};
		}
		public override void Assign(ArcEvent newValues)
		{
			base.Assign(newValues);
			ArcCamera n = newValues as ArcCamera;
			Move = n.Move;
			Rotate = n.Rotate;
			CameraType = n.CameraType;
			Duration = n.Duration;
		}

		public void Update(int Timing)
		{
			if (Timing > this.Timing + Duration)
			{
				Percent = 1;
				return;
			}
			else if (Timing <= this.Timing)
			{
				Percent = 0;
				return;
			}
			Percent = Mathf.Clamp((1f * Timing - this.Timing) / Duration, 0, 1);
			switch (CameraType)
			{
				case CameraEaseType.Qi:
					Percent = ArcAlgorithm.Qi(Percent);
					break;
				case CameraEaseType.Qo:
					Percent = ArcAlgorithm.Qo(Percent);
					break;
				case CameraEaseType.S:
					Percent = ArcAlgorithm.S(0, 1, Percent);
					break;
			}
		}
	}
}

