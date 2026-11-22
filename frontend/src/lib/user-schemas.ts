import { z } from "zod"
import { ROLES } from "@/types/auth"

const displayName = z.string().trim().min(1, "Display name is required.").max(120, "Display name is too long.")

const role = z.string().refine((value) => ROLES.some((item) => item === value), "Select a role.")

export const createUserSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "Email is required.")
    .max(256, "Email is too long.")
    .regex(/^\S+@\S+\.\S+$/, "Enter a valid email address."),
  displayName,

  password: z
    .string()
    .min(1, "Password is required.")
    .min(8, "Password must be at least 8 characters.")
    .max(128, "Password is too long.")
    .regex(/[A-Z]/, "Password must contain an uppercase letter.")
    .regex(/[a-z]/, "Password must contain a lowercase letter.")
    .regex(/[0-9]/, "Password must contain a digit.")
    .regex(/[^a-zA-Z0-9]/, "Password must contain a symbol."),
  role,
})

export const editUserSchema = z.object({ displayName, role })

export type CreateUserValues = z.infer<typeof createUserSchema>
export type EditUserValues = z.infer<typeof editUserSchema>

export const PASSWORD_HINT =
  "At least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a symbol. " +
  "Share it with the user through a secure channel: it is not shown again."