using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Drawing.UI.Advanced {

    public class SpriteElement : SceneElement {

        #region Properties

        #region Internals
        private double CachedInterval;

        private int GetCurrentFrame() {
            int ReturnValue = 0;
            if (InternalAnimation != null) {
                if (AnimationEnabled) {
                    ReturnValue = Convert.ToInt32(InternalAnimation.Value());
                } else {
                    ReturnValue = Convert.ToInt32(InternalAnimation.lastValue);
                }
            }
            return ReturnValue;
        }

        #endregion

        #region Sprite Status Constants
        public class SpriteStatus {
            public const byte Idle = 0;
            public const byte Active = 1;
        }
        #endregion

        public double FPS {
            get {
                return _FPS;
            }
            set {
                _FPS = value;
                CachedInterval = 1000d / _FPS;
                if (InternalAnimation != null) {
                    InternalAnimation.Duration = TimeSpan.FromMilliseconds(CachedInterval);
                }
            }
        }
        private double _FPS = 60d;

        public bool AutoRepeat {
            get {
                if (InternalAnimation != null) {
                    return InternalAnimation.AutoRepeat;
                } else {
                    return _AutoRepeat;
                }
            }
            set {
                if (InternalAnimation != null) {
                    InternalAnimation.AutoRepeat = value;
                }
                _AutoRepeat = value;
            }
        }
        private bool _AutoRepeat = false;

        public bool AnimationEnabled {
            get {
                return _AnimationEnabled;
            }
            set {
                _AnimationEnabled = value;
                switch (_AnimationEnabled) {
                    case true: {
                            StartAnimation();
                            break;
                        }
                    case false: {
                            StopAnimation();
                            break;
                        }
                }
            }
        }
        private bool _AnimationEnabled = false;

        public bool isAnimating {
            get {
                if (InternalAnimation == null) {
                    return false;
                } else {
                    return InternalAnimation.Running;
                }
            }
        }

        public int CurrentFrame {
            get {
                return _CurrentFrame;
            }
            set {
                if (_CurrentFrame != value) {
                    TimelineFrameChanged?.Invoke(ref value);
                }
                _CurrentFrame = value;
            }
        }
        private int _CurrentFrame = 0;

        public Dictionary<byte, ProjectZ.Shared.Content.SpriteCollection> AnimationCollections { get; set; } = new Dictionary<byte, ProjectZ.Shared.Content.SpriteCollection>();

        public byte ActiveCollectionIndex {
            get {
                return _ActiveCollectionIndex;
            }
            set {
                if (AnimationCollections.ContainsKey(value)) {
                    _ActiveCollectionIndex = value;
                    ActiveCollection = AnimationCollections[_ActiveCollectionIndex];
                    CachedInterval = 1000d / _FPS;
                    if (InternalAnimation != null) {
                        InternalAnimation.To = ActiveCollection.Count - 1;
                    }
                } else {
                    throw new Exception(string.Format("Animation Collection does not contain the key '{0}'.", new[] { value }));
                }
            }
        }
        private byte _ActiveCollectionIndex = 0;

        public ProjectZ.Shared.Content.SpriteCollection ActiveCollection {
            get {
                return _ActiveCollection;
            }
            set {
                _ActiveCollection = value;
                ActiveCollectionChanged?.Invoke(ref _ActiveCollection);
            }
        }
        private ProjectZ.Shared.Content.SpriteCollection _ActiveCollection;

        #endregion

        #region Animation Properties
        public ProjectZ.Shared.Animations.Properties.SpriteProgressProperty AnimationProgressProperty;

        #region Animation Instances
        private ProjectZ.Shared.Animations.DoubleAnimation _InternalAnimation;

        private ProjectZ.Shared.Animations.DoubleAnimation InternalAnimation {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get {
                return _InternalAnimation;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set {
                _InternalAnimation = value;
            }
        }

        public void StartAnimation() {
            InitializeAnimation();
            InternalAnimation.Start();
        }

        public void StopAnimation() {
            if (InternalAnimation != null) {
                InternalAnimation.Stop();
            }
        }

        private void InitializeAnimation() {
            InternalAnimation = new ProjectZ.Shared.Animations.DoubleAnimation(new ProjectZ.Shared.Animations.Easing.CircleEase(ProjectZ.Shared.Animations.Easing.EaseType.EaseInOut), 0d, (double)(ActiveCollection.Count - 1), TimeSpan.FromMilliseconds(CachedInterval), Scene.gameTime) { AutoRepeat = _AutoRepeat };


            this.BindAnimation(AnimationProgressProperty, InternalAnimation);
        }

        #endregion

        #endregion

        #region Events
        public event TimelineFrameChangedEventHandler TimelineFrameChanged;

        public delegate void TimelineFrameChangedEventHandler(ref int CurrentFrame);
        public event ActiveCollectionChangedEventHandler ActiveCollectionChanged;

        public delegate void ActiveCollectionChangedEventHandler(ref ProjectZ.Shared.Content.SpriteCollection ActiveCollection);
        #endregion

        protected internal override void Draw(GameTime gameTime) {
            if (ActiveCollection != null) {
                spriteBatch.Draw(ActiveCollection.get_Frame(CurrentFrame), Rectangle, Color.White);
            }
        }

        public override void Tick(GameTime gameTime) {
            base.Tick(gameTime);
        }

        #region Constructors
        public SpriteElement(Scene Scene) : base(Scene) {
            CachedInterval = 1000d / FPS;
            AnimationProgressProperty = new ProjectZ.Shared.Animations.Properties.SpriteProgressProperty(this);
            ActiveCollectionChanged += SpriteElement_ActiveCollectionChanged;
        }

        public SpriteElement(Scene Scene, SpriteBatch spriteBatch) : base(Scene, ref spriteBatch) {
            CachedInterval = 1000d / FPS;
            AnimationProgressProperty = new ProjectZ.Shared.Animations.Properties.SpriteProgressProperty(this);
            ActiveCollectionChanged += SpriteElement_ActiveCollectionChanged;
        }
        #endregion

        private void SpriteElement_ActiveCollectionChanged(ref ProjectZ.Shared.Content.SpriteCollection ActiveCollection) {
            if (InternalAnimation != null) {
                InternalAnimation.To = ActiveCollection.Count - 1;
            }
        }

    }

}