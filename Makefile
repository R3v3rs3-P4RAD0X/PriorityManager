# Priority Manager - Build & Release Automation
# Usage:
#   make build         - Build the mod DLL
#   make release       - Create release package (auto-detects version)
#   make clean         - Clean build artifacts
#   make install       - Install to RimWorld mods folder (if RIMWORLD_MODS_DIR set)

# Configuration
PROJECT_NAME := Priority Manager
SOURCE_DIR := Source
ASSEMBLIES_DIR := Assemblies
RELEASES_DIR := releases
BUILD_DIR := $(RELEASES_DIR)/build

# Detect version from About.xml
VERSION := $(shell grep -oP '<modVersion>\K[^<]+' "About/About.xml")

# RimWorld mods directory (set this in your environment or modify here)
RIMWORLD_MODS_DIR ?= $(HOME)/.steam/steam/steamapps/common/RimWorld/Mods

.PHONY: all build release clean install test help

# Default target
all: build

help:
	@echo "Priority Manager - Build & Release Automation"
	@echo ""
	@echo "Available targets:"
	@echo "  make build         - Build the mod DLL"
	@echo "  make release       - Create release package (current version: $(VERSION))"
	@echo "  make clean         - Clean build artifacts"
	@echo "  make install       - Install to RimWorld mods folder"
	@echo "  make test          - Build and install for testing"
	@echo "  make help          - Show this help message"
	@echo ""
	@echo "Current configuration:"
	@echo "  Version: $(VERSION)"
	@echo "  RimWorld Mods: $(RIMWORLD_MODS_DIR)"

# Build the mod
build:
	@echo "Building Priority Manager..."
	cd $(SOURCE_DIR) && dotnet build
	@echo "✓ Build complete: $(ASSEMBLIES_DIR)/PriorityManager.dll"

# Create release package
release: build
	@echo "Creating release package for v$(VERSION)..."
	@mkdir -p "$(BUILD_DIR)/$(PROJECT_NAME)/Assemblies"
	@mkdir -p "$(BUILD_DIR)/$(PROJECT_NAME)/About"
	@mkdir -p "$(BUILD_DIR)/$(PROJECT_NAME)/Defs"
	
	@echo "Copying files..."
	@cp -r About/* "$(BUILD_DIR)/$(PROJECT_NAME)/About/"
	@cp -r Defs/* "$(BUILD_DIR)/$(PROJECT_NAME)/Defs/"
	@cp $(ASSEMBLIES_DIR)/PriorityManager.dll "$(BUILD_DIR)/$(PROJECT_NAME)/Assemblies/"
	@cp README.md "$(BUILD_DIR)/$(PROJECT_NAME)/"
	@cp INSTALLATION.md "$(BUILD_DIR)/$(PROJECT_NAME)/" 2>/dev/null || true
	
	@echo "Creating archive..."
	@cd "$(BUILD_DIR)" && zip -r "../PriorityManager-v$(VERSION).zip" "$(PROJECT_NAME)" -q
	
	@echo "Cleaning up build directory..."
	@rm -rf "$(BUILD_DIR)"
	
	@echo "✓ Release package created: $(RELEASES_DIR)/PriorityManager-v$(VERSION).zip"
	@echo ""
	@ls -lh "$(RELEASES_DIR)/PriorityManager-v$(VERSION).zip"

# Clean build artifacts
clean:
	@echo "Cleaning build artifacts..."
	@cd $(SOURCE_DIR) && dotnet clean
	@rm -rf "$(BUILD_DIR)"
	@echo "✓ Clean complete"

# Install to RimWorld mods folder
install: build
	@echo "Installing to RimWorld mods folder..."
	@if [ ! -d "$(RIMWORLD_MODS_DIR)" ]; then \
		echo "Error: RimWorld mods directory not found: $(RIMWORLD_MODS_DIR)"; \
		echo "Set RIMWORLD_MODS_DIR environment variable or modify Makefile"; \
		exit 1; \
	fi
	
	@mkdir -p "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/Assemblies"
	@mkdir -p "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/About"
	@mkdir -p "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/Defs"
	
	@cp -r About/* "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/About/"
	@cp -r Defs/* "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/Defs/"
	@cp $(ASSEMBLIES_DIR)/PriorityManager.dll "$(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)/Assemblies/"
	
	@echo "✓ Installed to: $(RIMWORLD_MODS_DIR)/$(PROJECT_NAME)"

# Build and install for quick testing
test: build install
	@echo "✓ Ready for testing in RimWorld"

# Show current version
version:
	@echo "Current version: $(VERSION)"

# Verify no Harmony DLL in assemblies (common mistake)
verify:
	@echo "Verifying release integrity..."
	@if [ -f "$(ASSEMBLIES_DIR)/0Harmony.dll" ]; then \
		echo "⚠ WARNING: 0Harmony.dll found in Assemblies/ - this will cause conflicts!"; \
		echo "Run: rm $(ASSEMBLIES_DIR)/0Harmony.dll"; \
		exit 1; \
	fi
	@echo "✓ No Harmony DLL found in Assemblies"
	@echo "✓ Verification passed"

# Create release notes file
release-notes:
	@echo "Checking for release notes..."
	@if [ ! -f "$(RELEASES_DIR)/$(VERSION).md" ]; then \
		echo "⚠ Warning: Release notes not found at $(RELEASES_DIR)/$(VERSION).md"; \
		echo "Create this file before publishing to GitHub"; \
	else \
		echo "✓ Release notes found: $(RELEASES_DIR)/$(VERSION).md"; \
	fi

