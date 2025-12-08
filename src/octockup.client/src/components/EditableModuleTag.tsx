import { Box, Typography } from "@mui/material";
import { useState, type ChangeEvent, type KeyboardEvent } from "react";

interface EditableModuleTagProps {
  tag: string;
  onRename: (newTag: string) => Promise<void>;
}

export function EditableModuleTag({ tag, onRename }: EditableModuleTagProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editingTag, setEditingTag] = useState(tag);

  const handleRename = async () => {
    if (!editingTag.trim() || editingTag === tag) {
      setIsEditing(false);
      setEditingTag(tag);
      return;
    }
    try {
      await onRename(editingTag.trim());
      setIsEditing(false);
    } catch {
      setEditingTag(tag);
      setIsEditing(false);
    }
  };

  if (isEditing) {
    return (
      <Box
        component="input"
        autoFocus
        value={editingTag}
        onChange={(e: ChangeEvent<HTMLInputElement>) =>
          setEditingTag(e.target.value)
        }
        onBlur={handleRename}
        onKeyDown={(e: KeyboardEvent<HTMLInputElement>) => {
          if (e.key === "Enter") handleRename();
          if (e.key === "Escape") {
            setIsEditing(false);
            setEditingTag(tag);
          }
        }}
        sx={{
          textAlign: "center",
          maxWidth: 140,
          fontSize: "0.875rem",
          fontWeight: 500,
          border: "1px solid",
          borderColor: "primary.main",
          borderRadius: 1,
          px: 0.5,
          py: 0.25,
          outline: "none",
        }}
      />
    );
  }

  return (
    <Typography
      variant="subtitle2"
      noWrap
      title={tag}
      sx={{
        textAlign: "center",
        maxWidth: 140,
        cursor: "text",
      }}
      onDoubleClick={(e) => {
        e.stopPropagation();
        setIsEditing(true);
      }}
    >
      {tag}
    </Typography>
  );
}
