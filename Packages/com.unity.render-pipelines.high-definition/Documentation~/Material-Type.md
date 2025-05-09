# Material Type

The **Material Type** property allows you to give your Material a type, which allows you to customize it with different settings depending on the **Material Type** you select. Each has a different workflow and so use the **Material Type** that is most suitable for the Material you are creating.

| **Material Type**         | **Description**                                              |
| ------------------------- | ------------------------------------------------------------ |
| **Subsurface Scattering** | Applies the subsurface scattering workflow to the Material. Subsurface scattering simulates the way light interacts with and penetrates translucent objects, such as skin. When light penetrates the surface of a subsurface scattering Material, it scatters and blurs before exiting the surface at a different point. |
| **Standard**              | Applies the basic metallic Shader workflow to the Material. This is the default **Material Type**. |
| **Anisotropy**            | Applies the anisotropic workflow to the Material. The highlights of Anisotropic surfaces change in appearance as you view the Material from different angles. Use this **Material Type** to create Materials with anisotropic highlights. For example, brushed metal or velvet. |
| **Iridescence**           | Applies the Iridescence workflow to the Material. Iridescent surfaces appear to gradually change color as the angle of view or angle of illumination changes. Use this **Material Type** to create Materials like soap bubbles, iridescent metal, or insect wings. |
| **Specular Color**        | Applies the Specular Color workflow to the Material. Use this **Material Type** to create Materials with a coloured specular highlight. This is similar to the [built-in Specular Shader](https://docs.unity3d.com/Manual/StandardShaderMaterialParameterSpecular.html). |
| **Translucent**           | Applies the Translucent workflow to the Material. Use this **Material Type**, and a thickness map, to simulate a translucent object, such as a plant leaf. In contrast to **Subsurface Scattering** Materials, **Translucent** Materials do not blur light that transmits through the Material. |

![A detailed dragon statuette, rendered three times. The first dragon is iridescent, the second is a translucent green material, and the third has subsurface scattering.](Images/MaterialType1.png)

## Properties

Unity exposes different properties for your Material depending on the **Material Types** you select.

### Surface Options

| **Property**     | **Description**                                              |
| ---------------- | ------------------------------------------------------------ |
| **Transmission** | Enable the checkbox to make HDRP simulate the translucency of an object using a thickness map. Configure subsurface scattering and transmission settings using a [Diffusion Profile](diffusion-profile-reference.md). For more information, see documentation on [Subsurface Scattering](skin-and-diffusive-surfaces-subsurface-scattering.md).<br />This property only appears when you select **Subsurface Scattering** from the **Material Type** drop-down. |

### Surface Inputs

| **Property**                          | **Description**                                              |
| ------------------------------------- | ------------------------------------------------------------ |
| **Metallic**                          | Use the slider to adjust how metal-like the surface of your Material is (between 0 and 1). When a surface is more metallic, it reflects the environment more and its albedo color becomes less visible. At full metallic level, environmental reflections fully drive the surface color. When a surface is less metallic, its albedo color is clearer and any surface reflections are visible on top of the surface color, rather than obscuring it.<br />This property only appears when you select **Standard**, **Anisotropy**, or **Iridescence** from the **Material Type** drop-down. |
| **Diffusion Profile**                 | Assign a [Diffusion Profile](diffusion-profile-reference.md) to drive the behavior of subsurface scattering. To quickly view the currently selected Diffusion Profile’s Inspector, double click the Diffusion Profile Asset in the assign field. If you do not assign a Diffusion Profile, HDRP does not process the subsurface scattering.<br />This property only appears when you select **Subsurface Scattering** or **Translucent** from the **Material Type** drop-down. |
| **Subsurface Mask Map**               | Assign a grayscale Texture, with values from 0 to 1, that controls the strength of the blur effect across the Material. A texel with a value of 1 corresponds to full strength, while those with a value of 0 disables the Subsurface Scattering blur effect.<br />This property only appears when you select **Subsurface Scattering** from the **Material Type** drop-down. |
| **Subsurface Mask**                   | Use the slider to set the strength of the screen-space blur effect. If you set a **Subsurface Mask Map**, this acts as a multiplier for that map. If you do not set a Subsurface Mask Map, this increases the entire subsurface scattering effect on this Material.<br />This property only appears when you select **Subsurface Scattering** from the **Material Type** drop-down. |
| **Transmission Mask Map**             | Assign a grayscale Texture, with values from 0 to 1, that controls the strength of transmitted light across the Material. A texel with a value of 1 corresponds to full strength, while those with a value of 0 disables the Transmission effect.<br />This property only appears when **Material Type** is set to **Translucent** or if it is set to **Subsurface Scattering** and **translucent** option is enabled. |
| **Transmission Mask**                 | Use the slider to set the strength of the transmission effect. If you set a **Transmission Mask Map**, this acts as a multiplier for that map.<br />This property only appears when **Material Type** is set to **Translucent** or if it is set to **Subsurface Scattering** and **translucent** option is enabled. |
| **Thickness Map**                     | Assign a grayscale Texture, with values from 0 to 1, that correspond to the average thickness of the Mesh at the location of the texel. Higher values mean thicker areas, and thicker areas transmit less light.<br />This property only appears when you select **Subsurface Scattering** or **Translucent** from the **Material Type** drop-down. |
| **Thickness**                         | Use the slider to set the strength of the transmission effect. Multiplies the Thickness Map.<br />This property only appears when you select **Subsurface Scattering** or **Translucent** from the **Material Type** drop-down. |
| **Tangent Map**                       | Assign a Texture that defines the direction of the anisotropy effect of a pixel, in tangent space. This stretches the specular highlights in the given direction.<br />This property only appears when you select **ObjectSpace** from the **Normal Map Space** drop-down and **Anisotropy** from the **Material Type** drop-down. |
| **Tangent Map OS**                    | Assign a Texture that defines the direction of the anisotropy effect of a pixel, in object space. This stretches the specular highlights in the given direction.<br />This property only appears when you select **TangentSpace** from the **Normal Map Space** drop-down and **Anisotropy** from the **Material Type** drop-down. |
| **Anisotropy**                        | Use the slider to set the direction of the anisotropy effect. Negative values make the effect vertical, and positive values make the effect horizontal. This stretches the specular highlights in the given direction.<br />This property only appears when you select **Anisotropy** from the **Material Type** drop-down. |
| **Anisotropy Map**                    | Assign a Texture, with values from 0 to 1, that controls the strength of the anisotropy effect. HDRP only uses the red channel of this Texture to calculate the strength of the effect.<br />This property only appears when you select **Anisotropy** from the **Material Type** drop-down. |
| **Iridescence Mask**                  | Assign a Texture, with values from 0 to 1, that controls the strength of the iridescence effect. A texel with a value of 1 corresponds to full strength, while those with a value of 0 disables the iridescence effect.<br />This property only appears when you select **Iridescence** from the **Material Type** drop-down. |
| **Iridescence Layer Thickness map**   | Assign a Texture, with values from 0 to 1, that controls the thickness of the thin iridescence layer over the material. This modifies the color of the effect. Unit is micrometer multiplied by 3. A value of 1 is remapped to 3 micrometers or 3000 nanometers.<br />This property only appears when you select **Iridescence** from the **Material Type** drop-down. |
| **Iridescence Layer Thickness remap** | Use this min-max slider to remap the thickness values from the **Iridescence Layer Thickness map** to the range you specify. Rather than [clamping](https://docs.unity3d.com/ScriptReference/Mathf.Clamp.html) values to the new range, Unity condenses the original range down to the new range uniformly.<br />This property only appears when you select **Iridescence** from the **Material Type** drop-down. |
| **Specular Color**                    | Allows you to manually define the specular color. You can assign a Texture to define the specular color on a pixel level and use the color picker to select a global specular color for the Material. If you do both, HDRP multiplies each pixel of the Texture by the color you specify in the color picker.<br />This property only appears when you select **Specular Color** from the **Material Type** drop-down. |
| **Energy Conserving Specular Color**  | Enable the checkbox to make HDRP reduce the diffuse color of the Material if the specular effect is more intense. This makes the lighting of the Material more consistent, which makes the Material look more physically accurate.<br />This property only appears when you select **Specular Color** from the **Material Type** drop-down. |
