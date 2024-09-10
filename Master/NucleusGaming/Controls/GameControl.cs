﻿using Nucleus.Gaming;
using Nucleus.Gaming.Cache;
using Nucleus.Gaming.Controls;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Tools.GlobalWindowMethods;
using Nucleus.Gaming.UI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Nucleus.Coop
{
    public class GameControl : UserControl, IDynamicSized, IRadioControl
    {
        public GenericGameInfo GameInfo { get; set; }
        public UserGameInfo UserGameInfo { get; private set; }
        private PictureBox picture;
        private PictureBox playerIcon;
        public PictureBox FavoriteBox;
        private Label title;
        private Label players;
        public PictureBox HandlerUpdate;
        public PictureBox GameOptions;

        private Color radioSelectedBackColor;
        private Color defaultForeColor;

        public bool favorite;
        public string TitleText { get; set; }
        public string PlayerText { get; set; }

        private Bitmap favorite_Unselected;
        private Bitmap favorite_Selected;
        public System.Threading.Timer IsUpdateAvailableTimer;

        private Size maxSize;
        private Size minSize;
        private Point maxLoc;
        private Point minLoc;
        private Pen outlinePen;
        private SolidBrush fillBrush;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams handleparams = base.CreateParams;
                handleparams.ExStyle = 0x02000000;
                return handleparams;
            }
        }

        public GameControl(GenericGameInfo game, UserGameInfo userGame, bool favorite)
        {
            try
            {
                this.favorite = favorite;
                IniFile theme = Globals.ThemeConfigFile;

                string themePath = Globals.ThemeFolder;
                string[] rgb_SelectionColor = theme.IniReadValue("Colors", "Selection").Split(',');
                string[] rgb_MouseOverColor = theme.IniReadValue("Colors", "MouseOver").Split(',');
                string customFont = theme.IniReadValue("Font", "FontFamily");

                defaultForeColor = Color.FromArgb(255, int.Parse(theme.IniReadValue("Colors", "Font").Split(',')[0]), int.Parse(theme.IniReadValue("Colors", "Font").Split(',')[0]), int.Parse(theme.IniReadValue("Colors", "Font").Split(',')[0]));
                radioSelectedBackColor = Theme_Settings.SelectedBackColor;
                favorite_Unselected = ImageCache.GetImage(themePath + "favorite_unselected.png");
                favorite_Selected = ImageCache.GetImage(themePath + "favorite_selected.png");

                AutoScaleDimensions = new SizeF(96F, 96F);
                AutoScaleMode = AutoScaleMode.Dpi;
                AutoSize = false;
                Name = "GameControl";
                Size = new Size(209, 42);

                GameInfo = game;
                UserGameInfo = userGame;

                picture = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Transparent,
                    Name = "pictureBox"
                };

                playerIcon = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = ImageCache.GetImage(themePath + "players.png")
                };

                CustomToolTips.SetToolTip(playerIcon, "Number of players.", new int[] { 190, 0, 0, 0 }, new int[] { 255, 255, 255, 255 });

                title = new Label
                {
                    AutoSize = true,
                    Font = new Font(customFont, 8.25f, FontStyle.Bold, GraphicsUnit.Point, 0),
                };

                players = new Label
                {
                    AutoSize = true,
                    Font = new Font(customFont, 7, FontStyle.Bold, GraphicsUnit.Point, 0)
                };

                if (game == null)
                {
                    title.Text = "No games";
                    title.Font = new Font(customFont, 9, FontStyle.Bold, GraphicsUnit.Point, 0);
                    Visible = false;
                }
                else
                {
                    title.Text = GameInfo.GameName;
                    if (GameInfo.MaxPlayers > 2)
                    {
                        players.Text = "2 - " + GameInfo.MaxPlayers;
                    }
                    else
                    {
                        players.Text = GameInfo.MaxPlayers.ToString();
                    }
                }

                FavoriteBox = new PictureBox
                {
                    Name = "favorite",
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Transparent,
                    Cursor = Theme_Settings.Hand_Cursor,
                    Tag = "no_parent_click"
                };

                GameOptions = new PictureBox
                {
                    Name = "gameOptions",
                    BackColor = BackColor = Color.Transparent,
                    BackgroundImage = ImageCache.GetImage(themePath + "game_options.png"),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Cursor = Theme_Settings.Hand_Cursor,
                    Tag = "no_parent_click"
                };

                HandlerUpdate = new PictureBox
                {
                    Name = "updateButton",
                    BackColor = Color.Transparent,
                    BackgroundImage = ImageCache.GetImage(themePath + "update.png"),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Cursor = Theme_Settings.Hand_Cursor,
                    Visible = false,
                    Tag = "no_parent_click"
                };

                FavoriteBox.Image = favorite ? favorite_Selected : favorite_Unselected;
                FavoriteBox.Click += new EventHandler(FavoriteBox_Click);

                title.BackColor = Color.Transparent;

                TitleText = title.Text;

                PlayerText = players.Text;

                players.BackColor = Color.Transparent;
                playerIcon.BackColor = Color.Transparent;

                BackColor = Color.Transparent;

                Controls.Add(picture);
                Controls.Add(title);
                Controls.Add(players);
                Controls.Add(GameOptions);
                Controls.Add(HandlerUpdate);

                if (title.Text != "No games")
                {
                    Controls.Add(playerIcon);
                    Controls.Add(FavoriteBox);
                }

                MouseEnter += ZoomInPicture;
                MouseLeave += ZoomOutPicture;

                FavoriteBox.MouseEnter += ZoomInPicture;
                FavoriteBox.MouseLeave += ZoomOutPicture;

                GameOptions.MouseEnter += ZoomInPicture;
                GameOptions.MouseLeave += ZoomOutPicture;

                HandlerUpdate.MouseEnter += ZoomInPicture;
                HandlerUpdate.MouseLeave += ZoomOutPicture;

                outlinePen = new Pen(Color.FromArgb(15, 255, 255, 255));
                fillBrush = new SolidBrush(Color.FromArgb(50, 20, 20, 20));

                DPIManager.Register(this);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("ERROR - " + ex.Message + "\n\nSTACKTRACE: " + ex.StackTrace);
            }
            ///Set a different title color if a handler update is available using a timer(need to wait for the hub to return the value).
            IsUpdateAvailableTimer = new System.Threading.Timer(IsUpdateAvailable_Tick, this, 1500, 1550);
        }
        ~GameControl()
        {
            DPIManager.Unregister(this);
        }

        public void UpdateSize(float scale)
        {
            if (IsDisposed)
            {
                DPIManager.Unregister(this);
                return;
            }

            SuspendLayout();

            int margin = 40;
            int border = 4;

            picture.Size = new Size((int)((margin - border) * scale), (int)((margin - border) * scale));
            picture.Location = new Point(border, border);

            title.Text = TitleText;
            players.Text = PlayerText;

            title.AutoSize = true;
            title.MaximumSize = new Size((int)(209 * scale) - picture.Width - (border * 2), 0);

            playerIcon.Size = new Size(players.Size.Height + 2, players.Size.Height + 2 );

            title.Location = new Point(picture.Right + border, picture.Location.Y);
            playerIcon.Location = new Point(title.Location.X + 2, title.Bottom + 3);
            players.Location = new Point(playerIcon.Right + 2, playerIcon.Bottom - players.Height);

            if (picture.Height < playerIcon.Bottom)//more than one title row
            {
                Height = playerIcon.Bottom + border;
                picture.Location = new Point(picture.Location.X, Height / 2 - picture.Height / 2);
            }
            else// one row
            {
                Height = picture.Bottom + border;//adjust the control Height
            }

            FavoriteBox.Size = new Size(playerIcon.Width, playerIcon.Width);

            float favoriteX = (209 * scale) - (playerIcon.Width + 5);
            float favoriteY = Height - (FavoriteBox.Height + 5);
            FavoriteBox.Location = new Point((int)favoriteX, (int)favoriteY);

            GameOptions.Size = FavoriteBox.Size;
            GameOptions.Location = new Point(FavoriteBox.Left - (GameOptions.Width + 5), FavoriteBox.Location.Y);

            HandlerUpdate.Size = FavoriteBox.Size; 
            HandlerUpdate.Location = new Point(GameOptions.Left - (HandlerUpdate.Width + 5), FavoriteBox.Location.Y);

            maxSize = new Size((FavoriteBox.Right - HandlerUpdate.Location.X) + 3, FavoriteBox.Height);
            minSize = new Size((FavoriteBox.Right - GameOptions.Location.X) + 3, FavoriteBox.Height);

            maxLoc = new Point(HandlerUpdate.Location.X,FavoriteBox.Top);
            minLoc = new Point(GameOptions.Location.X, FavoriteBox.Top); 

            CustomToolTips.SetToolTip(FavoriteBox, "Add or remove this game from your favorites.", new int[] { 190, 0, 0, 0 }, new int[] { 255, 255, 255, 255 });
            CustomToolTips.SetToolTip(GameOptions, "Game options menu.", new int[] { 190, 0, 0, 0 }, new int[] { 255, 255, 255, 255 });
            CustomToolTips.SetToolTip(HandlerUpdate, "Update game handler.", new int[] { 190, 0, 0, 0 }, new int[] { 255, 255, 255, 255 });

            ResumeLayout();
        }

        private void IsUpdateAvailable_Tick(object state)
        {
            try
            {
                Control topLevel = TopLevelControl;

                if (topLevel != null)
                {
                    if (topLevel.IsHandleCreated && GameInfo != null)
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            if (FavoriteBox.Visible)
                            {
                                if (GameInfo.UpdateAvailable && GameInfo.MetaInfo.CheckUpdate)
                                {
                                    title.ForeColor = Color.FromArgb(255, 196, 145, 18);
                                    HandlerUpdate.Visible = true;
                                }
                                else
                                {
                                    title.ForeColor = defaultForeColor;
                                    HandlerUpdate.Visible = false;
                                }

                                Invalidate(false);
                            }
                        });
                    }
                }
            }
            catch
            {
                IsUpdateAvailableTimer.Dispose();
                Console.WriteLine("GameControl was Disposed!");
            }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            Control c = e.Control;

            if ((string)c.Tag != "no_parent_click")
            {
                c.Click += C_Click;
            }
            
            c.MouseEnter += C_MouseEnter;
            c.MouseLeave += C_MouseLeave;

            DPIManager.Update(this);
        }

        private void FavoriteBox_Click(object sender, EventArgs e)
        {
            MouseEventArgs arg = e as MouseEventArgs;

            if (arg.Button == MouseButtons.Right)
            {
                return;
            }

            bool selected = FavoriteBox.Image.Equals(favorite_Selected);
            FavoriteBox.Image = selected ? favorite_Unselected : favorite_Selected;
            UserGameInfo.Game.MetaInfo.Favorite = selected ? false : true;

            GameManager.Instance.SaveUserProfile();
        }

        private void C_MouseEnter(object sender, EventArgs e)
        {
            Control c = sender as Control;

            if ((string)c.Tag != "no_parent_click")
            {
                OnMouseEnter(e);
            }
            else
            {
                c.Size = new Size(c.Width += 3, c.Height += 3);
                c.Location = new Point(c.Location.X - 1, c.Location.Y - 1);
            }
        }

        private void C_MouseLeave(object sender, EventArgs e)
        {
            Control c = sender as Control;

            if ((string)c.Tag != "no_parent_click")
            {
                OnMouseLeave(e);
            }
            else
            {
                c.Size = new Size(c.Width -= 3, c.Height -= 3);
                c.Location = new Point(c.Location.X + 1, c.Location.Y + 1);
            }
        }

        private void ZoomInPicture(object sender, EventArgs e)
        {       
            picture.Size = new Size(picture.Width += 3, picture.Height += 3);
            picture.Location = new Point(picture.Location.X - 1, picture.Location.Y - 1);
            picture.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, picture.Width, picture.Height, 10,10));
        }

        private void ZoomOutPicture(object sender, EventArgs e)
        {
            picture.Size = new Size(picture.Width -= 3, picture.Height -= 3);
            picture.Location = new Point(picture.Location.X + 1, picture.Location.Y + 1);
            picture.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, picture.Width, picture.Height, 10, 10));
        }

        private void C_Click(object sender, EventArgs e)
        {          
            OnClick(e);            
        }

        public Image Image
        {
            get => picture.Image;
            set => picture.Image = value;
        }

        public override string ToString()
        {
            return Text;
        }

        private bool isSelected;

        public void RadioSelected()
        {
            isSelected = true;
        }

        public void RadioUnselected()
        {
            isSelected = false;
        }

        public void UserOver()
        {
        }

        public void UserLeave()
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle gradientBrushbounds = new Rectangle(0, 0, Width, Height / 3);
            Rectangle bounds = new Rectangle(0, 0, Width, Height);

            Color color = isSelected ? radioSelectedBackColor : Color.Transparent;

            LinearGradientBrush lgb =
            new LinearGradientBrush(gradientBrushbounds, Color.Transparent, color, 57f);

            ColorBlend topcblend = new ColorBlend(3);
            topcblend.Colors = new Color[3] { color, color, Color.Transparent };
            topcblend.Positions = new float[3] { 0f, 0.5f, 1.0f };

            lgb.InterpolationColors = topcblend;
            lgb.SetBlendTriangularShape(.5f, 1.0f);
            e.Graphics.FillRectangle(lgb, bounds);

            lgb.Dispose();

            if (FavoriteBox.Visible)
            {
                Size oulineRect = HandlerUpdate.Visible ? maxSize : minSize;

                int locX = HandlerUpdate.Visible ? maxLoc.X - 5 : minLoc.X - 5;
                int locY = maxLoc.Y - 3;
                int width = oulineRect.Width + 6;
                int height = oulineRect.Height + 6;

                Rectangle inputTextBack = new Rectangle(locX, locY, width, height);
                Rectangle inputTextBackOutline = new Rectangle(locX, locY, width, height);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

                e.Graphics.FillPath(fillBrush, FormGraphicsUtil.MakeRoundedRect(inputTextBack, 10, 10, true, false, false, true));
                e.Graphics.DrawPath(outlinePen, FormGraphicsUtil.MakeRoundedRect(inputTextBackOutline, 10, 10, true, false, false, true));
            }
        }
        
    }
}