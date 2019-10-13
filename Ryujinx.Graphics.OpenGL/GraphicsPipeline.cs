using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Blend;
using Ryujinx.Graphics.GAL.Color;
using Ryujinx.Graphics.GAL.DepthStencil;
using Ryujinx.Graphics.GAL.InputAssembler;
using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    class GraphicsPipeline : IGraphicsPipeline
    {
        private Program _program;

        private VertexArray _vertexArray;
        private Framebuffer _framebuffer;

        private IntPtr _indexBaseOffset;

        private DrawElementsType _elementsType;

        private PrimitiveType _primitiveType;

        private int  _stencilFrontMask;
        private bool _depthMask;
        private bool _depthTest;
        private bool _hasDepthBuffer;

        private TextureView _unit0Texture;

        private ClipOrigin _clipOrigin;

        private uint[] _componentMasks;

        internal GraphicsPipeline()
        {
            _clipOrigin = ClipOrigin.LowerLeft;
        }

        public void BindBlendState(int index, BlendDescriptor blend)
        {
            if (!blend.Enable)
            {
                GL.Disable(IndexedEnableCap.Blend, index);

                return;
            }

            GL.BlendEquationSeparate(
                index,
                blend.ColorOp.Convert(),
                blend.AlphaOp.Convert());

            GL.BlendFuncSeparate(
                index,
                (BlendingFactorSrc) blend.ColorSrcFactor.Convert(),
                (BlendingFactorDest)blend.ColorDstFactor.Convert(),
                (BlendingFactorSrc) blend.AlphaSrcFactor.Convert(),
                (BlendingFactorDest)blend.AlphaDstFactor.Convert());

            GL.Enable(IndexedEnableCap.Blend, index);
        }

        public void BindIndexBuffer(BufferRange buffer, IndexType type)
        {
            _elementsType = type.Convert();

            _indexBaseOffset = (IntPtr)buffer.Offset;

            EnsureVertexArray();

            _vertexArray.SetIndexBuffer((Buffer)buffer.Buffer);
        }

        public void BindProgram(IProgram program)
        {
            _program = (Program)program;

            _program.Bind();
        }

        public void BindSampler(int index, ShaderStage stage, ISampler sampler)
        {
            int unit = _program.GetTextureUnit(stage, index);

            if (unit != -1 && sampler != null)
            {
                ((Sampler)sampler).Bind(unit);
            }
        }

        public void BindTexture(int index, ShaderStage stage, ITexture texture)
        {
            int unit = _program.GetTextureUnit(stage, index);

            if (unit != -1 && texture != null)
            {
                if (unit == 0)
                {
                    _unit0Texture = ((TextureView)texture);
                }
                else
                {
                    ((TextureView)texture).Bind(unit);
                }
            }
        }

        public void BindStorageBuffers(int index, ShaderStage stage, BufferRange[] buffers)
        {
            BindBuffers(index, stage, buffers, isStorage: true);
        }

        public void BindUniformBuffers(int index, ShaderStage stage, BufferRange[] buffers)
        {
            BindBuffers(index, stage, buffers, isStorage: false);
        }

        private void BindBuffers(int index, ShaderStage stage, BufferRange[] buffers, bool isStorage)
        {
            for (int bufferIndex = 0; bufferIndex < buffers.Length; bufferIndex++, index++)
            {
                int bindingPoint = isStorage
                    ? _program.GetStorageBufferBindingPoint(stage, index)
                    : _program.GetUniformBufferBindingPoint(stage, index);

                if (bindingPoint == -1)
                {
                    continue;
                }

                BufferRange buffer = buffers[bufferIndex];

                BufferRangeTarget target = isStorage
                    ? BufferRangeTarget.ShaderStorageBuffer
                    : BufferRangeTarget.UniformBuffer;

                if (buffer.Buffer == null)
                {
                    GL.BindBufferRange(target, bindingPoint, 0, IntPtr.Zero, 0);

                    continue;
                }

                int bufferHandle = ((Buffer)buffer.Buffer).Handle;

                IntPtr bufferOffset = (IntPtr)buffer.Offset;

                GL.BindBufferRange(
                    target,
                    bindingPoint,
                    bufferHandle,
                    bufferOffset,
                    buffer.Size);
            }
        }

        public void BindVertexAttribs(VertexAttribDescriptor[] vertexAttribs)
        {
            EnsureVertexArray();

            _vertexArray.SetVertexAttributes(vertexAttribs);
        }

        public void BindVertexBuffers(VertexBufferDescriptor[] vertexBuffers)
        {
            EnsureVertexArray();

            _vertexArray.SetVertexBuffers(vertexBuffers);
        }

        public void ClearRenderTargetColor(int index, uint componentMask, ColorF color)
        {
            GL.ColorMask(
                index,
                (componentMask & 1) != 0,
                (componentMask & 2) != 0,
                (componentMask & 4) != 0,
                (componentMask & 8) != 0);

            float[] colors = new float[] { color.Red, color.Green, color.Blue, color.Alpha };

            GL.ClearBuffer(ClearBuffer.Color, index, colors);

            RestoreComponentMask(index);
        }

        public void ClearRenderTargetColor(int index, uint componentMask, ColorSI color)
        {
            GL.ColorMask(
                index,
                (componentMask & 1u) != 0,
                (componentMask & 2u) != 0,
                (componentMask & 4u) != 0,
                (componentMask & 8u) != 0);

            int[] colors = new int[] { color.Red, color.Green, color.Blue, color.Alpha };

            GL.ClearBuffer(ClearBuffer.Color, index, colors);

            RestoreComponentMask(index);
        }

        public void ClearRenderTargetColor(int index, uint componentMask, ColorUI color)
        {
            GL.ColorMask(
                index,
                (componentMask & 1u) != 0,
                (componentMask & 2u) != 0,
                (componentMask & 4u) != 0,
                (componentMask & 8u) != 0);

            uint[] colors = new uint[] { color.Red, color.Green, color.Blue, color.Alpha };

            GL.ClearBuffer(ClearBuffer.Color, index, colors);

            RestoreComponentMask(index);
        }

        public void ClearRenderTargetDepthStencil(
            float depthValue,
            bool  depthMask,
            int   stencilValue,
            int   stencilMask)
        {
            bool stencilMaskChanged =
                stencilMask != 0 &&
                stencilMask != _stencilFrontMask;

            bool depthMaskChanged = depthMask && depthMask != _depthMask;

            if (stencilMaskChanged)
            {
                GL.StencilMaskSeparate(StencilFace.Front, stencilMask);
            }

            if (depthMaskChanged)
            {
                GL.DepthMask(depthMask);
            }

            if (depthMask && stencilMask != 0)
            {
                GL.ClearBuffer(ClearBufferCombined.DepthStencil, 0, depthValue, stencilValue);
            }
            else if (depthMask)
            {
                GL.ClearBuffer(ClearBuffer.Depth, 0, ref depthValue);
            }
            else if (stencilMask != 0)
            {
                GL.ClearBuffer(ClearBuffer.Stencil, 0, ref stencilValue);
            }

            if (stencilMaskChanged)
            {
                GL.StencilMaskSeparate(StencilFace.Front, _stencilFrontMask);
            }

            if (depthMaskChanged)
            {
                GL.DepthMask(_depthMask);
            }
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            if (!_program.IsLinked)
            {
                return;
            }

            PrepareForDraw();

            if (firstInstance == 0 && instanceCount == 1)
            {
                if (_primitiveType == PrimitiveType.Quads)
                {
                    for (int offset = 0; offset < vertexCount; offset += 4)
                    {
                        GL.DrawArrays(PrimitiveType.TriangleFan, firstVertex + offset, 4);
                    }
                }
                else if (_primitiveType == PrimitiveType.QuadStrip)
                {
                    GL.DrawArrays(PrimitiveType.TriangleFan, firstVertex, 4);

                    for (int offset = 2; offset < vertexCount; offset += 2)
                    {
                        GL.DrawArrays(PrimitiveType.TriangleFan, firstVertex + offset, 4);
                    }
                }
                else
                {
                    GL.DrawArrays(_primitiveType, firstVertex, vertexCount);
                }

                // GL.DrawArrays(_primitiveType, firstVertex, vertexCount);
            }
            else if (firstInstance == 0)
            {
                GL.DrawArraysInstanced(_primitiveType, firstVertex, vertexCount, instanceCount);
            }
            else
            {
                GL.DrawArraysInstancedBaseInstance(
                    _primitiveType,
                    firstVertex,
                    vertexCount,
                    instanceCount,
                    firstInstance);
            }
        }

        public void DrawIndexed(
            int indexCount,
            int instanceCount,
            int firstIndex,
            int firstVertex,
            int firstInstance)
        {
            if (!_program.IsLinked)
            {
                return;
            }

            PrepareForDraw();

            int firstIndexOffset = firstIndex;

            switch (_elementsType)
            {
                case DrawElementsType.UnsignedShort: firstIndexOffset *= 2; break;
                case DrawElementsType.UnsignedInt:   firstIndexOffset *= 4; break;
            }

            IntPtr indexBaseOffset = _indexBaseOffset + firstIndexOffset;

            if (firstInstance == 0 && firstVertex == 0 && instanceCount == 1)
            {
                GL.DrawElements(_primitiveType, indexCount, _elementsType, indexBaseOffset);
            }
            else if (firstInstance == 0 && instanceCount == 1)
            {
                GL.DrawElementsBaseVertex(
                    _primitiveType,
                    indexCount,
                    _elementsType,
                    indexBaseOffset,
                    firstVertex);
            }
            else if (firstInstance == 0 && firstVertex == 0)
            {
                GL.DrawElementsInstanced(
                    _primitiveType,
                    indexCount,
                    _elementsType,
                    indexBaseOffset,
                    instanceCount);
            }
            else if (firstInstance == 0)
            {
                GL.DrawElementsInstancedBaseVertex(
                    _primitiveType,
                    indexCount,
                    _elementsType,
                    indexBaseOffset,
                    instanceCount,
                    firstVertex);
            }
            else if (firstVertex == 0)
            {
                GL.DrawElementsInstancedBaseInstance(
                    _primitiveType,
                    indexCount,
                    _elementsType,
                    indexBaseOffset,
                    instanceCount,
                    firstInstance);
            }
            else
            {
                GL.DrawElementsInstancedBaseVertexBaseInstance(
                    _primitiveType,
                    indexCount,
                    _elementsType,
                    indexBaseOffset,
                    instanceCount,
                    firstVertex,
                    firstInstance);
            }
        }

        public void DrawIndirect(BufferRange buffer, ulong offset, int drawCount, int stride)
        {

        }

        public void DrawIndexedIndirect(BufferRange buffer, ulong offset, int drawCount, int stride)
        {

        }

        public void SetBlendColor(ColorF color)
        {
            GL.BlendColor(color.Red, color.Green, color.Blue, color.Alpha);
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            if ((enables & PolygonModeMask.Point) != 0)
            {
                GL.Enable(EnableCap.PolygonOffsetPoint);
            }
            else
            {
                GL.Disable(EnableCap.PolygonOffsetPoint);
            }

            if ((enables & PolygonModeMask.Line) != 0)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
            }
            else
            {
                GL.Disable(EnableCap.PolygonOffsetLine);
            }

            if ((enables & PolygonModeMask.Fill) != 0)
            {
                GL.Enable(EnableCap.PolygonOffsetFill);
            }
            else
            {
                GL.Disable(EnableCap.PolygonOffsetFill);
            }

            if (enables == 0)
            {
                return;
            }

            GL.PolygonOffset(factor, units);
            // GL.PolygonOffsetClamp(factor, units, clamp);
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            GL.DepthFunc((DepthFunction)depthTest.Func.Convert());

            _depthMask = depthTest.WriteEnable;
            _depthTest = depthTest.TestEnable;

            UpdateDepthTest();
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            if (!enable)
            {
                GL.Disable(EnableCap.CullFace);

                return;
            }

            GL.CullFace(face.Convert());

            GL.Enable(EnableCap.CullFace);
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            GL.FrontFace(frontFace.Convert());
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            if (!enable)
            {
                GL.Disable(EnableCap.PrimitiveRestart);

                return;
            }

            GL.PrimitiveRestartIndex(index);

            GL.Enable(EnableCap.PrimitiveRestart);
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _primitiveType = topology.Convert();
        }

        public void SetRenderTargetColorMasks(uint[] componentMasks)
        {
            _componentMasks = (uint[])componentMasks.Clone();

            for (int index = 0; index < componentMasks.Length; index++)
            {
                RestoreComponentMask(index);
            }
        }

        public void SetRenderTargets(ITexture color3D, ITexture depthStencil)
        {
            EnsureFramebuffer();

            TextureView color = (TextureView)color3D;

            for (int index = 0; index < color.DepthOrLayers; index++)
            {
                _framebuffer.AttachColor(index, color, index);
            }

            TextureView depthStencilView = (TextureView)depthStencil;

            _framebuffer.AttachDepthStencil(depthStencilView);

            _framebuffer.SetDrawBuffers(color.DepthOrLayers);

            _hasDepthBuffer = depthStencil != null && depthStencilView.Format != Format.S8Uint;

            UpdateDepthTest();
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            EnsureFramebuffer();

            for (int index = 0; index < colors.Length; index++)
            {
                TextureView color = (TextureView)colors[index];

                _framebuffer.AttachColor(index, color);
            }

            TextureView depthStencilView = (TextureView)depthStencil;

            _framebuffer.AttachDepthStencil(depthStencilView);

            _framebuffer.SetDrawBuffers(colors.Length);

            _hasDepthBuffer = depthStencil != null && depthStencilView.Format != Format.S8Uint;

            UpdateDepthTest();
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            if (!stencilTest.TestEnable)
            {
                GL.Disable(EnableCap.StencilTest);

                return;
            }

            GL.StencilOpSeparate(
                StencilFace.Front,
                stencilTest.FrontSFail.Convert(),
                stencilTest.FrontDpFail.Convert(),
                stencilTest.FrontDpPass.Convert());

            GL.StencilFuncSeparate(
                StencilFace.Front,
                (StencilFunction)stencilTest.FrontFunc.Convert(),
                stencilTest.FrontFuncRef,
                stencilTest.FrontFuncMask);

            GL.StencilMaskSeparate(StencilFace.Front, stencilTest.FrontMask);

            GL.StencilOpSeparate(
                StencilFace.Back,
                stencilTest.BackSFail.Convert(),
                stencilTest.BackDpFail.Convert(),
                stencilTest.BackDpPass.Convert());

            GL.StencilFuncSeparate(
                StencilFace.Back,
                (StencilFunction)stencilTest.BackFunc.Convert(),
                stencilTest.BackFuncRef,
                stencilTest.BackFuncMask);

            GL.StencilMaskSeparate(StencilFace.Back, stencilTest.BackMask);

            GL.Enable(EnableCap.StencilTest);

            _stencilFrontMask = stencilTest.FrontMask;
        }

        public void SetViewports(int first, Viewport[] viewports)
        {
            bool flipY = false;

            float[] viewportArray = new float[viewports.Length * 4];

            double[] depthRangeArray = new double[viewports.Length * 2];

            for (int index = 0; index < viewports.Length; index++)
            {
                int viewportElemIndex = index * 4;

                Viewport viewport = viewports[index];

                viewportArray[viewportElemIndex + 0] = viewport.Region.X;
                viewportArray[viewportElemIndex + 1] = viewport.Region.Y;

                // OpenGL does not support per-viewport flipping, so
                // instead we decide that based on the viewport 0 value.
                // It will apply to all viewports.
                if (index == 0)
                {
                    flipY = viewport.Region.Height < 0;
                }

                if (viewport.SwizzleY == ViewportSwizzle.NegativeY)
                {
                    flipY = !flipY;
                }

                viewportArray[viewportElemIndex + 2] = MathF.Abs(viewport.Region.Width);
                viewportArray[viewportElemIndex + 3] = MathF.Abs(viewport.Region.Height);

                depthRangeArray[index * 2 + 0] = viewport.DepthNear;
                depthRangeArray[index * 2 + 1] = viewport.DepthFar;
            }

            GL.ViewportArray(first, viewports.Length, viewportArray);

            GL.DepthRangeArray(first, viewports.Length, depthRangeArray);

            SetOrigin(flipY ? ClipOrigin.UpperLeft : ClipOrigin.LowerLeft);
        }

        private void SetOrigin(ClipOrigin origin)
        {
            if (_clipOrigin != origin)
            {
                _clipOrigin = origin;

                GL.ClipControl(origin, ClipDepthMode.NegativeOneToOne);
            }
        }

        private void EnsureVertexArray()
        {
            if (_vertexArray == null)
            {
                _vertexArray = new VertexArray();

                _vertexArray.Bind();
            }
        }

        private void EnsureFramebuffer()
        {
            if (_framebuffer == null)
            {
                _framebuffer = new Framebuffer();

                _framebuffer.Bind();

                GL.Enable(EnableCap.FramebufferSrgb);
            }
        }

        private void UpdateDepthTest()
        {
            // Enabling depth operations is only valid when we have
            // a depth buffer, otherwise it's not allowed.
            if (_hasDepthBuffer)
            {
                if (_depthTest)
                {
                    GL.Enable(EnableCap.DepthTest);
                }
                else
                {
                    GL.Disable(EnableCap.DepthTest);
                }

                GL.DepthMask(_depthMask);
            }
            else
            {
                GL.Disable(EnableCap.DepthTest);

                GL.DepthMask(false);
            }
        }

        private void PrepareForDraw()
        {
            _vertexArray.Validate();

            if (_unit0Texture != null)
            {
                _unit0Texture.Bind(0);
            }
        }

        private void RestoreComponentMask(int index)
        {
            GL.ColorMask(
                index,
                (_componentMasks[index] & 1u) != 0,
                (_componentMasks[index] & 2u) != 0,
                (_componentMasks[index] & 4u) != 0,
                (_componentMasks[index] & 8u) != 0);
        }

        public void RebindProgram()
        {
            _program?.Bind();
        }
    }
}
