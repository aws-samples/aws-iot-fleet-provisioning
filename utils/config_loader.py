# ------------------------------------------------------------------------------
# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: MIT-0 
# -----------------------------------------

from configparser import ConfigParser


class Config:
    def __init__(self, config_file_path):
        self.cf = ConfigParser()
        self.cf.optionxform = str
        self.config_file_path = config_file_path
        self.cf.read(self.config_file_path)

    def get_section(self, section):
        return dict(self.cf.items(section))
