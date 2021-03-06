﻿//#define TEST
#define PRINT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.ComponentModel;
using System.Data;
using OpenTK.Platform;
using System.Diagnostics;
using System.Runtime.InteropServices;

#region Assembly Collisions
using TK = OpenTK.Graphics.OpenGL;
using GL = OpenTK.Graphics.OpenGL.GL;
#endregion

namespace ParallelComputedCollisionDetection
{
    public class Window : GameWindow
    {
        #region Global Members
        MouseState old_mouse;
        float offsetX = 0f, offsetY = 0f;
        Vector3 eye, target, up;
        float mouse_sensitivity=0.2f;
        Matrix4 modelView;
        float scale_factor = 1;
        float rotation_speed = 2.5f;
        public static int fov;
        //public int sphere_precision = 20;
        float coord_transf;
        float wp_scale_factor = 3;
        public float grid_edge = 3;
        public int number_of_bodies = 16;
        int tiles;
        KeyboardState old_key;
        bool xRot;
        bool yRot;
        bool zRot;
        bool ortho = true;
        int picked = -1;

        long elaspedTime;
        int frames;
        float updateInterval = 250f;
        Stopwatch timeSinceStart;

        MouseState mouse;
        float[] mat_specular = { 1.0f, 1.0f, 1.0f, 1.0f };
        float[] mat_shininess = { 50.0f };
        float[] light_position = { 1f, 5f, 0.1f, 0.0f };
        float[] light_ambient = { 0.5f, 0.5f, 0.5f, 1.0f };
        float oldX, oldY; 
        float[][] colors = new float[][]{   new float[]{1f, 0f, 0f, 0.0f}, new float[]{0f, 1f, 0f, 0.0f}, new float[]{0f, 0f, 1f, 0.0f},
                                            new float[]{1f, 1f, 1f, 0.0f}, new float[]{1f, 1f, 0f, 0.0f}, new float[]{1f, 0f, 1f, 0.0f},
                                            new float[]{1f, 1f, 0f, 0.0f}, new float[]{0.9f, 0.5f, 0.1f, 0.0f}, new float[]{0f, 1f, 1f, 0.0f},
                                            new float[]{1f, 0f, 1f, 0.0f}, new float[]{0f, 1f, 1f, 0.0f}, new float[]{0.7f, 0.5f, 0.4f, 0.0f}};
        float width;
        float height;
        char view = '0';
        double gizmosOffsetX = 10.5;
        double gizmosOffsetY = 10.5;
        double gizmosOffsetZ = 10.5;

        public List<Body> bodies;

        float aspect_ratio;
        Matrix4 perspective;
        #endregion

        public Window()
            : base(1366, 768, new GraphicsMode(32, 0, 0, 4), "Parallel Computed Collision Detection")
        {
            this.WindowState = WindowState.Fullscreen;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);

            GL.Material(TK.MaterialFace.Front, TK.MaterialParameter.Specular, mat_specular);
            GL.Material(TK.MaterialFace.Front, TK.MaterialParameter.Shininess, mat_shininess);
            GL.Light(TK.LightName.Light0, TK.LightParameter.Position, light_position);
            GL.Light(TK.LightName.Light0, TK.LightParameter.Ambient, light_ambient);
            GL.Light(TK.LightName.Light0, TK.LightParameter.Diffuse, mat_specular);

            GL.Enable(TK.EnableCap.CullFace);
            GL.Enable(TK.EnableCap.DepthTest);
            GL.ShadeModel(TK.ShadingModel.Smooth);
            GL.Enable(TK.EnableCap.ColorMaterial);
            GL.Enable(TK.EnableCap.Blend);
            GL.BlendFunc(TK.BlendingFactorSrc.SrcAlpha, TK.BlendingFactorDest.OneMinusSrcAlpha);
            GL.PolygonMode(TK.MaterialFace.FrontAndBack, TK.PolygonMode.Fill);
            
            GL.Viewport(0, 0, Width, Height);
            aspect_ratio = Width / (float)Height;
            checkAspectRatio();
            width *= wp_scale_factor;
            height *= wp_scale_factor;

            VSync = VSyncMode.On;
            eye = new Vector3(0, 0, height * 1.5f);
            target = new Vector3(0, 0, 0);
            up = new Vector3(0, 1, 0);
            fov = (int)Math.Round(height * 0.75f);

            old_mouse = OpenTK.Input.Mouse.GetState();
            old_key = OpenTK.Input.Keyboard.GetState();

            bodies = new List<Body>();
            
            generateRandomBodies(number_of_bodies, true);
            calculateGridEdge();
            foreach (Body body in bodies)
            {
                body.updateBoundingSphere();
                body.getBSphere().checkForCellIntersection();
            }

            mouse = OpenTK.Input.Mouse.GetState();
            coord_transf = Screen.PrimaryScreen.Bounds.Height / 27f;

            timeSinceStart = new Stopwatch();
            timeSinceStart.Start();
            elaspedTime = timeSinceStart.ElapsedMilliseconds;
            Program.cd.InitializePlatformPropertiesAndDevices();
            Program.cd.deviceSpecs();
            Program.cd.ReadAllSources();
            Program.cd.CreateCollisionCellArray();
            Program.t.Start();
            while (Program.ready == false) ;
        }

        protected override void OnUnload(EventArgs e)
        {
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);

            aspect_ratio = Width / (float)Height;
            perspective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
            GL.MatrixMode(TK.MatrixMode.Projection);
            GL.LoadMatrix(ref perspective);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
#if TEST

#else
            bringPanelOnTop();

            if (Keyboard[OpenTK.Input.Key.Escape])
            {
                MethodInvoker mi = delegate
                {
                    Program.db.close();
                };
                try
                {
                    Program.db.BeginInvoke(mi);
                }
                catch(Exception ex)
                { Console.WriteLine("Error encountered while closing the application - " + ex.Message); }
                this.Exit();
            }

            if (Keyboard[OpenTK.Input.Key.Space])
                if (WindowState != WindowState.Fullscreen)
                    WindowState = WindowState.Fullscreen;
                else
                    WindowState = WindowState.Maximized;
            checkMouseInput();
            checkKeyboardInput();
            foreach (Body body in bodies)
                body.updateBoundingSphere();
        #endif
        }

        void checkMouseInput()
        {
            mouse = OpenTK.Input.Mouse.GetState();

            if (mouse.IsButtonDown(MouseButton.Left) && !old_mouse.IsButtonDown(MouseButton.Left))
                pickBody();
            /*else if (!mouse.IsButtonDown(MouseButton.Left))
                picked = -1;*/
            else if (mouse.IsButtonDown(MouseButton.Left) && old_mouse.IsButtonDown(MouseButton.Left) && picked >= 0)
            {
                float deltaX = Cursor.Position.X - oldX;
                float deltaY = Cursor.Position.Y - oldY;
                moveBody(deltaX, deltaY);
            }
            else if (mouse.IsButtonDown(MouseButton.Middle) && !old_mouse.IsButtonDown(MouseButton.Middle) && picked >= 0)
            {
                MethodInvoker mi = delegate
                {
                    Program.db.comp_rtb.Text = "loading...";
                    Program.db.rtb.Text = "";
                    Program.db.fps_rtb.Text = "";
                };
                try
                {
                    Program.db.rtb.BeginInvoke(mi);
                    //Program.db.comp_rtb.BeginInvoke(mi);
                    //Program.db.fps_rtb.BeginInvoke(mi);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error encountered while updating OCL data - " + e.Message);
                }
                Program.cd.CreateCollisionCellArray();
                showInfo();
            }

            if (mouse.IsButtonDown(MouseButton.Right))
            {
                CursorVisible = false;
                offsetX += (mouse.X - old_mouse.X) * mouse_sensitivity;
                offsetY += (mouse.Y - old_mouse.Y) * mouse_sensitivity;
            }
            else
                CursorVisible = true;
            
            if(ortho){
                scale_factor += (mouse.WheelPrecise - old_mouse.WheelPrecise) * 0.5f;
                if(scale_factor > 50f)
                    scale_factor = 50f;
                else if(scale_factor < 0.5f)
                    scale_factor = 0.5f;
            }
            else{
                eye.Z -= (mouse.WheelPrecise - old_mouse.WheelPrecise);
                if (eye.Z < 3)
                    eye.Z = 3;
            }
            old_mouse = mouse;
            oldX = Cursor.Position.X;
            oldY = Cursor.Position.Y;
        }

        void checkKeyboardInput()
        {
            if (Keyboard[Key.R] && !old_key.IsKeyDown(Key.R))
            {
                
                MethodInvoker mi = delegate
                {
                    Program.db.comp_rtb.Text = "loading...";
                    Program.db.rtb.Text = "";
                    Program.db.fps_rtb.Text = "";
                };
                try
                {
                    Program.db.BeginInvoke(mi);
                }
                catch (Exception e)
                {
                    Console.Write("Error encountered while generating random bodies - " + e.Message);
                }
                picked = -1; //this resolves the IndexOutOfBoundException
                generateRandomBodies(number_of_bodies, true);
                //Program.cd.CreateCollisionCellArray();
                
                //picked = -1;
                showInfo();
            }
            if (Keyboard[Key.Left])
                offsetX -= rotation_speed;
            if (Keyboard[Key.Right])
                offsetX += rotation_speed;
            if (Keyboard[Key.Up])
                offsetY += rotation_speed;
            if (Keyboard[Key.Down])
                offsetY -= rotation_speed;

            if (Keyboard[Key.V] && !old_key.IsKeyDown(Key.V))
            {
                if (ortho)
                    ortho = false;
                else
                    ortho = true;
            }

            #region XYZ Grid Rotations
            if (Keyboard[Key.Z] || zRot)
            {
                zRot = true;
                if (offsetX > 0f)
                    offsetX -= rotation_speed;
                else if (offsetX < 0f)
                    offsetX += rotation_speed;
                if (offsetY > 0f)
                    offsetY -= rotation_speed;
                else if (offsetY < 0f)
                    offsetY += rotation_speed;
                if (offsetX  > -2f && offsetX < 2f)
                    offsetX = 0f;
                if (offsetY > -2f && offsetY < 2f)
                    offsetY = 0f;
            }

            if (Keyboard[Key.Y] || yRot)
            {
                yRot = true;
                if (offsetX > 0f)
                    offsetX -= rotation_speed;
                else if (offsetX < 0f)
                    offsetX += rotation_speed;
                if (offsetY > 90f)
                    offsetY -= rotation_speed;
                else if (offsetY < 90f)
                    offsetY += rotation_speed;
                if (offsetX > -2f && offsetX < 2f)
                    offsetX = 0f;
                if (offsetY > 88f && offsetY < 92f)
                    offsetY = 90f;
            }

            if (Keyboard[Key.X] || xRot)
            {
                xRot = true;
                if (offsetX > -90f)
                    offsetX -= rotation_speed;
                else if (offsetX < -90f)
                    offsetX += rotation_speed;
                if (offsetY > 0f)
                    offsetY -= rotation_speed;
                else if (offsetY < 0f)
                    offsetY += rotation_speed;
                if (offsetX < -88f && offsetX > -92f)
                    offsetX = -90f;
                if (offsetY > -2f && offsetY < 2f)
                    offsetY = 0f;
            }

            if (offsetX == -90f && offsetY == 0f)
            {
                xRot = false;
                view = 'x';
            }

            else if (offsetX == 0f && offsetY == 90f)
            {
                yRot = false;
                view = 'y';
            }

            else if (offsetX == 0f && offsetY == 0f)
            {
                zRot = false;
                view = 'z';
            }

            else
                view = '0';
            #endregion

            old_key = OpenTK.Input.Keyboard.GetState();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
#if TEST
#else
            GL.Clear(TK.ClearBufferMask.DepthBufferBit | TK.ClearBufferMask.ColorBufferBit);

            if (ortho)
            {
                modelView = Matrix4.CreateOrthographic(width, height, -height, height);
                GL.MatrixMode(TK.MatrixMode.Projection);
                GL.PushMatrix();
                GL.LoadMatrix(ref modelView);
                GL.Scale(scale_factor, scale_factor, scale_factor);
            }
            else
            {
                modelView = Matrix4.LookAt(eye, target, up);
                GL.MatrixMode(TK.MatrixMode.Modelview);
                GL.PushMatrix();
                GL.LoadMatrix(ref modelView);
            }
            GL.PushMatrix();
            GL.Rotate(offsetX, 0.0f, 1.0f, 0.0f);
            GL.Rotate(offsetY, 1.0f, 0.0f, 0.0f);

            DrawGrid();

            #region Draw 3D Polyhedrons
            
            GL.Color3(0.0, 0.5, 1.0);

            GL.Enable(TK.EnableCap.Light0);
            GL.Enable(TK.EnableCap.Lighting);

            int i = 0;
            foreach (Body body in bodies)
            {
                float[] color = colors[i % colors.Count()];
                color[3] = 0.9f;
                GL.Color4(color);
                body.Draw();
                color[3] = 0.25f;
                GL.Color4(color);
                body.getBSphere().Draw();
                i++;
            }

            if (picked != -1)
            {
                GL.Color3(colors[picked % colors.Count()]);
                GL.PolygonMode(TK.MaterialFace.FrontAndBack, TK.PolygonMode.Line);
                foreach (Body body in bodies.ElementAt(picked).getBSphere().cells)
                    body.Draw();
                GL.PolygonMode(TK.MaterialFace.FrontAndBack, TK.PolygonMode.Fill);
            }

            GL.Disable(TK.EnableCap.Lighting);
            GL.Disable(TK.EnableCap.Light0);
            #endregion

            #region Draw Gizmos
            drawCollisionIntervals();
            #endregion

            GL.PopMatrix();
            GL.PopMatrix();

            SwapBuffers();

            showFPS();
            //showInfo();
#endif
        }

        void DrawGrid()
        {
            int half_fov = fov / 2;
            float offset;

            GL.Color4(1f, 0f, 0f, 1f);

            //grid y
            GL.Begin(PrimitiveType.LineLoop);
            {
                GL.Vertex3(-half_fov, -half_fov, half_fov);
                GL.Vertex3(half_fov, -half_fov, half_fov);
                GL.Vertex3(half_fov, -half_fov, -half_fov);
                GL.Vertex3(-half_fov, -half_fov, -half_fov);
            }
            GL.End();

            for (int i = 1; i < tiles; i++)
            {
                offset = (float)grid_edge * i;

                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(offset - half_fov, -half_fov, half_fov);
                    GL.Vertex3(offset - half_fov, -half_fov, -half_fov);

                    GL.Vertex3(-half_fov, -half_fov, offset - half_fov);
                    GL.Vertex3(half_fov, -half_fov, offset - half_fov);
                }
                GL.End();
            }

            GL.Color4(0f, 1f, 0f, 1f);

            //grid x
            GL.Begin(PrimitiveType.LineLoop);
            {
                GL.Vertex3(-half_fov, half_fov, half_fov);
                GL.Vertex3(-half_fov, half_fov, -half_fov);
                GL.Vertex3(-half_fov, -half_fov, -half_fov);
                GL.Vertex3(-half_fov, -half_fov, half_fov);
            }
            GL.End();

            for (int i = 1; i < tiles; i++)
            {
                offset = (float)grid_edge * i;

                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(-half_fov, -half_fov, offset - half_fov);
                    GL.Vertex3(-half_fov, half_fov, offset - half_fov);

                    GL.Vertex3(-half_fov, offset - half_fov, half_fov);
                    GL.Vertex3(-half_fov, offset - half_fov, -half_fov);
                }
                GL.End();
            }

            GL.Color4(0f, 0f, 1f, 1f);

            //grid z
            GL.Begin(PrimitiveType.LineLoop);
            {
                GL.Vertex3(-half_fov, -half_fov, -half_fov);
                GL.Vertex3(half_fov, -half_fov, -half_fov);
                GL.Vertex3(half_fov, half_fov, -half_fov);
                GL.Vertex3(-half_fov, half_fov, -half_fov);
            }
            GL.End();

            for (int i = 1; i < tiles; i++)
            {
                offset = (float)grid_edge * i;

                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(offset - half_fov, -half_fov, -half_fov);
                    GL.Vertex3(offset - half_fov, half_fov, -half_fov);

                    GL.Vertex3(-half_fov, offset - half_fov, -half_fov);
                    GL.Vertex3(half_fov, offset - half_fov, -half_fov);
                }
                GL.End();
            }

        }

        void checkAspectRatio()
        {
            if (aspect_ratio == 4 / 3f)
            {
                width = 4f;
                height = 3f;
                return;
            }
            if (aspect_ratio == 16 / 10f)
            {
                width = 16f;
                height = 10f;
                return;
            }
            else
            {
                width = 16f;
                height = 9f;
            }
        }

        void drawCollisionIntervals()
        {
            if (view == '0' || picked == - 1)
                return;

            GL.LineWidth(4.0f);
            colors[picked % (colors.Count())][3] = 0.5f;
            GL.Color4(colors[picked % (colors.Count())]);
            Vector3 pos = bodies[picked].getPos();
            Sphere bsphere = bodies[picked].getBSphere();

            if (view == 'z')
            {
                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(pos.X - bsphere.radius, -gizmosOffsetY, 10);
                    GL.Vertex3(pos.X + bsphere.radius, -gizmosOffsetY, 10);

                    GL.Vertex3(-gizmosOffsetX, pos.Y + bsphere.radius, 10);
                    GL.Vertex3(-gizmosOffsetX, pos.Y - bsphere.radius, 10);
                }
                GL.End();
            }

            else if (view == 'x')
            {
                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(10, -gizmosOffsetY, pos.Z - bsphere.radius);
                    GL.Vertex3(10, -gizmosOffsetY, pos.Z + bsphere.radius);

                    GL.Vertex3(10, pos.Y + bsphere.radius, gizmosOffsetZ);
                    GL.Vertex3(10, pos.Y - bsphere.radius, gizmosOffsetZ);
                }
                GL.End();
            }

            else
            {
                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Vertex3(-gizmosOffsetX, 10, pos.Z + bsphere.radius);
                    GL.Vertex3(-gizmosOffsetX, 10, pos.Z - bsphere.radius);

                    GL.Vertex3(pos.X - bsphere.radius, 10, gizmosOffsetZ);
                    GL.Vertex3(pos.X + bsphere.radius, 10, gizmosOffsetZ);
                }
                GL.End();
            }
            GL.LineWidth(1.0f);
        }

        void moveBody(float deltaX, float deltaY)
        {
            Body[] bodies_ = bodies.ToArray();
            Vector3 pos = bodies_[picked].getPos();
            switch (view)
            {
                case 'x':
                    pos.Z -= deltaX / coord_transf;
                    pos.Y -= deltaY / coord_transf;
                    break;
                case 'y':
                    pos.X += deltaX / coord_transf;
                    pos.Z += deltaY / coord_transf;
                    break;
                case 'z':
                    pos.X += deltaX / coord_transf;
                    pos.Y -= deltaY / coord_transf;
                    break;
                default:
                    break;
            }
            bodies_[picked].setPos(pos);
            //bodies_[picked].getBSphere().checkForCellIntersection();
            showInfo();
        }

        void pickBody()
        {
            picked = -1;
            float depthTest = -15;
            Body[] bodies_ = bodies.ToArray();
            switch (view)
            {
                case 'x':
                    for (int i = 0; i < number_of_bodies; i++)
                    {
                        Sphere bsphere = bodies_[i].getBSphere();
                        Vector3 pos = bodies_[i].getPos();
                        if (Math.Abs(((Cursor.Position.X - Screen.PrimaryScreen.Bounds.Width * 0.5) / coord_transf)
                            + pos.Z) < bsphere.radius
                            && Math.Abs(((-(Cursor.Position.Y - Screen.PrimaryScreen.Bounds.Height * 0.5)) / coord_transf)
                            - pos.Y) < bsphere.radius
                            && pos.X > depthTest)
                        {
                            depthTest = pos.X;
                            picked = i;
                        }
                    }
                    break;

                case 'y':
                    for (int i = 0; i < number_of_bodies; i++)
                    {
                        Sphere bsphere = bodies_[i].getBSphere();
                        Vector3 pos = bodies_[i].getPos();
                        if (Math.Abs(((Cursor.Position.X - Screen.PrimaryScreen.Bounds.Width * 0.5) / coord_transf)
                            - pos.X) < bsphere.radius
                            && Math.Abs(((-(Cursor.Position.Y - Screen.PrimaryScreen.Bounds.Height * 0.5)) / coord_transf)
                            + pos.Z) < bsphere.radius
                            && pos.Y > depthTest)
                        {
                            depthTest = pos.Y;
                            picked = i;
                        }
                    }
                    break;

                case 'z':
                    for (int i = 0; i < number_of_bodies; i++)
                    {
                        Sphere bsphere = bodies_[i].getBSphere();
                        Vector3 pos = bodies_[i].getPos();
                        if (Math.Abs(((Cursor.Position.X - Screen.PrimaryScreen.Bounds.Width * 0.5) / coord_transf)
                            - pos.X) < bsphere.radius
                            && Math.Abs(((-(Cursor.Position.Y - Screen.PrimaryScreen.Bounds.Height * 0.5)) / coord_transf)
                            - pos.Y) < bsphere.radius
                            && pos.Z > depthTest)
                        {
                            depthTest = pos.Z;
                            picked = i;
                        }
                    }
                    break;

                default:
                    picked = -1;
                    return;
            }

            //if (picked != -1)
                //bodies.ElementAt(picked).getBSphere().checkForCellIntersection();
            //bodies_[picked].getBSphere().checkForCellIntersection();
            showInfo();
        }

        void generateRandomBodies(int n, bool cube)
        {
            bodies.Clear();
            Random rand = new Random((int)DateTime.Now.TimeOfDay.TotalMilliseconds);
            int safe_area = (int)(fov * 0.5 - grid_edge * 0.5);
            for (int i = 0; i < n; i++)
            {
                float x = rand.Next(-safe_area, safe_area);
                float y = rand.Next(-safe_area, safe_area);
                float z = rand.Next(-safe_area, safe_area);
                float length = rand.Next(7, 10) * 0.25f;
                float height, width;
                if (cube)
                {
                    height = length;
                    width = length;
                }
                else
                {
                    height = rand.Next(7, 10) * 0.25f;
                    width = rand.Next(7, 10) * 0.25f;
                }
                int nBodies = bodies.Count;
                //Console.Write(nBodies + "\n");
                bodies.Add(new Parallelepiped(new Vector3(x, y, z), length, height, width, 0f, nBodies));
            }
            calculateGridEdge();
        }

        void calculateGridEdge()
        {
            float maxRadius = 0;
            foreach (Body body in bodies)
                if (maxRadius < body.getBSphere().radius)
                    maxRadius = body.getBSphere().radius;
            //grid_edge = Math.Sqrt(maxRadius * 2) * 1.5;
            grid_edge = maxRadius * 2;
            tiles = (int)(fov / grid_edge);
            grid_edge = fov / (float)tiles;
            //Console.Write("\nfov: " + fov.ToString() + ", tiles: " + tiles.ToString() + ", grid_edge: " + grid_edge.ToString() + "\n");
        }

        void showInfo()
        {
#if PRINT
            {
                MethodInvoker mi = delegate
                {
                    if (picked == -1)
                    {
                        Program.db.rtb.Text = "";
                        //if (Program.db.getComp_RTB().Text != "loading...")
                        Program.db.comp_rtb.Text = "";
                    }
                    else
                    {
                        Body pBody = bodies.ElementAt(picked);
                        string binValue = Convert.ToString(pBody.getBSphere().ctrl_bits, 2);
                        char[] bits = binValue.PadLeft(16, '0').ToCharArray();
                        binValue = "";
                        string binValue2 = Convert.ToString(Program.cd.array[picked].ctrl_bits, 2);
                        char[] bits2 = binValue2.PadLeft(16, '0').ToCharArray();
                        binValue2 = "";
                        for (int i = 0; i < 16; i++)
                        {
                            if (i % 4 == 0)
                            {
                                binValue += " ";
                                binValue2 += " ";
                            }
                            binValue += bits[i];
                            binValue2 += bits2[i];
                        }
                        string cellTypesIntersected = "";
                        Sphere s = pBody.getBSphere();
                        if ((s.ctrl_bits & 1) == 1)
                            cellTypesIntersected += "1 ";
                        if ((s.ctrl_bits & 2) == 2)
                            cellTypesIntersected += "2 ";
                        if ((s.ctrl_bits & 4) == 4)
                            cellTypesIntersected += "3 ";
                        if ((s.ctrl_bits & 8) == 8)
                            cellTypesIntersected += "4 ";
                        if ((s.ctrl_bits & 16) == 16)
                            cellTypesIntersected += "5 ";
                        if ((s.ctrl_bits & 32) == 32)
                            cellTypesIntersected += "6 ";
                        if ((s.ctrl_bits & 64) == 64)
                            cellTypesIntersected += "7 ";
                        if ((s.ctrl_bits & 128) == 128)
                            cellTypesIntersected += "8 ";
                        BodyData bd = Program.cd.array[picked];
                        #region PRINT DEBUG
                        Program.db.rtb.Text = "Body[" + picked + "]:"
                                                    + "\n\tposition: (" + pBody.getPos().X.ToString("0.00")
                                                    + ", " + pBody.getPos().Y.ToString("0.00")
                                                    + ", " + pBody.getPos().Z.ToString("0.00") + ")"
                                                    + "\n\tradius: " + s.radius.ToString("0.00")
                                                    + "\n\thCell pos: " + s.cellPos.ToString()
                                                    + "\n\thCell type: " + ((s.ctrl_bits & (15<<8))>>8)
                                                    + "\n\t# of cells inters.: " + s.cells.Count.ToString()
                                                    + "\n\tcTypes inters.: " + cellTypesIntersected
                                                    + "\n\tBodyData struct size: " + Marshal.SizeOf(bd);

                        Program.db.comp_rtb.Text = "\n\tCPU vs OCL"
                                                        + "\n\n                       HASH\n"
                                                        + "\n[0]\t" + s.cellArray[0].ToString() + "\t" + bd.cellIDs[0]
                                                        + "\n[1]\t" + s.cellArray[1].ToString() + "\t" + bd.cellIDs[1]
                                                        + "\n[2]\t" + s.cellArray[2].ToString() + "\t" + bd.cellIDs[2]
                                                        + "\n[3]\t" + s.cellArray[3].ToString() + "\t" + bd.cellIDs[3]
                                                        + "\n[4]\t" + s.cellArray[4].ToString() + "\t" + bd.cellIDs[4]
                                                        + "\n[5]\t" + s.cellArray[5].ToString() + "\t" + bd.cellIDs[5]
                                                        + "\n[6]\t" + s.cellArray[6].ToString() + "\t" + bd.cellIDs[6]
                                                        + "\n[7]\t" + s.cellArray[7].ToString() + "\t" + bd.cellIDs[7]
                                                        + "\n\n               CONTROL BITS\n"
                                                        + "\n(int)\t" + s.ctrl_bits.ToString() + "\t" + bd.ctrl_bits.ToString()
                                                        + "\n(bin)\tOCL " + binValue2 + "\n\tCPU " + binValue;
                        #endregion
                    }
                };
                try { Program.db.BeginInvoke(mi); }
                catch(Exception e) {
                    Console.WriteLine("Error encountered while updating info - " + e.Message);
                }
            }
#else
            MethodInvoker mi = delegate
                {
                Program.db.comp_rtb.Text = "";
                };
            try { Program.db.comp_rtb.BeginInvoke(mi); }
            catch (Exception e)
            {
                Console.WriteLine("Error encountered while updating info - " + e.Message);
            }
#endif
        }

        public void showFPS()
        {
            long deltaTime = timeSinceStart.ElapsedMilliseconds - elaspedTime;
            ++frames;

            if (deltaTime >= updateInterval)
            {
                elaspedTime = timeSinceStart.ElapsedMilliseconds;
                int fps = (int)((frames * 1000f) / deltaTime);
                frames = 0;
                string format = System.String.Format("fps: " + fps);

                MethodInvoker mi = delegate
                {
                    Program.db.Text = format;

                    if (fps < 30 && fps >= 10)
                        Program.db.fps_rtb.ForeColor = Color.Yellow;
                    else if (fps < 10)
                        Program.db.ForeColor = Color.Red;
                    else
                        Program.db.ForeColor = Color.LawnGreen;
                };
                try
                {
                    Program.db.fps_rtb.BeginInvoke(mi);
                }
                catch (Exception e)
                {
                    Console.Write("Error encountered while updating fps counter - " + e.Message);
                }
            }
        }

        public void bringPanelOnTop()
        {
            MethodInvoker mi = delegate
                {
                    Program.db.BringToFront();
                };
            Program.db.BeginInvoke(mi);
        }
    }
}
